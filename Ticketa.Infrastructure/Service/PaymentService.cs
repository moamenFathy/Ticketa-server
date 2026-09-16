using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Text.Json;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Core.Interfaces.IServices;
using Ticketa.Core.Specifications;

namespace Ticketa.Infrastructure.Service
{
  public class PaymentService(
      IUnitOfWork uow,
      IBookingService bookingService,
      IEmailService emailService,
      IQrCodeService qrCodeService,
      IConfiguration configuration,
      ILogger<PaymentService> logger,
      PaymentIntentService? paymentIntentService = null,
      RefundService? refundService = null) : IPaymentService
  {
    private readonly IUnitOfWork _uow = uow;
    private readonly IBookingService _bookingService = bookingService;
    private readonly IEmailService _emailService = emailService;
    private readonly IQrCodeService _qrCodeService = qrCodeService;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<PaymentService> _logger = logger;
    private readonly PaymentIntentService _paymentIntentService = paymentIntentService ?? new PaymentIntentService();
    private readonly RefundService _refundService = refundService ?? new RefundService();
    private static char RowToLetter(int row) => (char)('A' + row - 1);

    public async Task<BookingResultDto> ConfirmAsync(string paymentIntentId, string userId, CancellationToken ct = default)
    {
      var paymentIntent = await _paymentIntentService.GetAsync(paymentIntentId, cancellationToken: ct);

      if (paymentIntent.Status != "succeeded")
        return BookingResultDto.Failure("Payment has not been completed.");

      if (paymentIntent.Metadata["userId"] != userId)
        return BookingResultDto.Failure("Unauthorized access.");

      var showtimeId = int.Parse(paymentIntent.Metadata["showtimeId"]);
      var seats = JsonSerializer.Deserialize<List<SeatDto>>(paymentIntent.Metadata["seats"])!;

      var payment = await _uow.Payments.GetAsync(p => p.StripePaymentIntentId == paymentIntentId);

      if (payment is not null && payment.Status == PaymentStatus.Completed)
      {
        _logger.LogInformation("Payment {IntentId} already processed (Completed), returning cached result", paymentIntentId);
        return BookingResultDto.Success(payment.BookingReference ?? "", payment.TotalAmount);
      }

      var bookingDto = new BookingCreateDto { ShowtimeId = showtimeId, Seats = seats };
      var result = await _bookingService.CreateAsync(bookingDto, userId, ct);

      if (!result.Succeeded && result.ConflictingSeats.Count > 0)
      {
        await _refundService.CreateAsync(
            new RefundCreateOptions { PaymentIntent = paymentIntentId },
            cancellationToken: ct
          );

        if (payment is not null)
        {
          payment.Status = PaymentStatus.Refunded;
          payment.RefundedAt = DateTime.UtcNow;
          await _uow.SaveAsync();
        }
      }
      else if (result.Succeeded && payment is not null)
      {
        payment.BookingReference = result.BookingReference;
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        try
        {
          var details = await _bookingService.GetByReferenceAsync(result.BookingReference!, ct);

          if (details is not null)
          {
            var clientBaseUrl = _configuration["ClientSettings:BaseUrl"] ?? "http://localhost:5173";
            var scanUrl = $"{clientBaseUrl.TrimEnd('/')}/bookings/{result.BookingReference}";
            var qrBytes = _qrCodeService.GeneratePng(scanUrl);
            const string cid = "ticket-qr";
            var backdropUrl = string.IsNullOrEmpty(details.MovieBackdropPath)
                ? null
                : $"https://image.tmdb.org/t/p/w780{details.MovieBackdropPath}";

            var html = EmailTemplates.BookingConfirmation(
                details.CustomerFirstName,
                details.MovieTitle,
                details.StartsAt,
                details.HallName,
                details.Seats.Select(s => $"{RowToLetter(s.Row)}{s.SeatNumber}"),
                details.TotalAmount,
                result.BookingReference!,
                cid,
                backdropUrl);

            await _emailService.SendEmailWithInlineImageAsync(
                details.CustomerEmail,
                "Your Ticketa Booking Confirmation",
                html,
                qrBytes,
                cid,
                ct);
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Ticket email failed for booking {Reference}", result.BookingReference);
        }
      }

      return result;
    }

    public async Task<PaymentIntentResultDto> CreateIntentAsync(CreatePaymentIntentDto dto, string userId, CancellationToken ct = default)
    {
      var spec = new ShowtimeByIdSpecification(dto.ShowtimeId);
      var showtime = await _uow.Showtimes.GetEntityWithSpecAsync(spec);
      if (showtime is null) return null;

      var template = HallTypeHelper.GetTemplate(showtime.Hall.Type);
      dto.Seats = dto.Seats.OrderBy(s => s.Row).ThenBy(s => s.SeatNumber).ToList();
      var seatHash = ComputeSeatHash(dto.Seats);

      var dedupSpec = new PaymentSpecification(userId, dto.ShowtimeId, seatHash);
      var existing = await _uow.Payments.GetEntityWithSpecAsync(dedupSpec, ct);
      if (existing is not null)
      {
        return new PaymentIntentResultDto
        {
          ClientSecret = existing.ClientSecret,
          PaymentIntentId = existing.StripePaymentIntentId,
          TotalAmount = existing.TotalAmount
        };
      }

      decimal totalAmount = 0;
      var paymentSeats = new List<PaymentSeat>();
      foreach (var seat in dto.Seats)
      {
        var category = template.RowCategoryMap[seat.Row];
        var multiplier = HallTypeHelper.GetPriceMultiplier(category);
        var unitPrice = showtime.Price * multiplier;
        totalAmount += unitPrice;
        paymentSeats.Add(new PaymentSeat { Row = seat.Row, SeatNumber = seat.SeatNumber, UnitPrice = unitPrice });
      }

      var metaData = new Dictionary<string, string>
      {
        ["userId"] = userId,
        ["showtimeId"] = dto.ShowtimeId.ToString(),
        ["seats"] = JsonSerializer.Serialize(dto.Seats)
      };

      var options = new PaymentIntentCreateOptions
      {
        Amount = (long)(totalAmount * 100),
        Currency = "AED",
        Metadata = metaData,
        AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
        {
          Enabled = true,
        },
      };

      var requestOptions = new RequestOptions
      {
        IdempotencyKey = Guid.NewGuid().ToString("N")
      };

      var paymnetIntent = await _paymentIntentService.CreateAsync(options, requestOptions, ct);

      var payment = new Payment
      {
        StripePaymentIntentId = paymnetIntent.Id,
        ClientSecret = paymnetIntent.ClientSecret,
        UserId = userId,
        ShowtimeId = dto.ShowtimeId,
        TotalAmount = totalAmount,
        Currency = "AED",
        Status = PaymentStatus.Pending,
        SeatHash = seatHash,
        SeatCount = dto.Seats.Count,
        CreatedAt = DateTime.UtcNow,
        PaymentSeats = paymentSeats
      };

      await _uow.Payments.CreateAsync(payment);
      await _uow.SaveAsync();

      return new PaymentIntentResultDto
      {
        ClientSecret = paymnetIntent.ClientSecret,
        PaymentIntentId = paymnetIntent.Id,
        TotalAmount = totalAmount
      };
    }

    private static string ComputeSeatHash(IEnumerable<SeatDto> seats)
    {
      return string.Join(",", seats
          .OrderBy(s => s.Row).ThenBy(s => s.SeatNumber)
          .Select(s => $"{s.Row}:{s.SeatNumber}"));
    }

  }
}
