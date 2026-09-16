# 🏛️ ADR 001: SQL Server vs. PostgreSQL

## 📌 Status
**Accepted**

---

## 🎯 Context
When designing **Ticketa**, choosing the primary relational database management system (RDBMS) required balancing .NET ecosystem synergy, performance profiling capabilities, concurrency controls, and target hosting environment constraints (MonsterASP / IIS).

The primary candidates considered were:
1. **Microsoft SQL Server** (with `Microsoft.EntityFrameworkCore.SqlServer`)
2. **PostgreSQL** (with `Npgsql.EntityFrameworkCore.PostgreSQL`)

---

## ⚖️ Evaluation & Comparison

| Consideration | Microsoft SQL Server | PostgreSQL (Npgsql) |
| :--- | :--- | :--- |
| **.NET & EF Core Synergy** | First-party Microsoft support, zero translation impedance, native tooling. | Excellent community provider (`Npgsql`), rich extension ecosystem. |
| **Index Profiling & Diagnostics** | SQL Server Management Studio (SSMS) execution plans with clear Scan/Seek/Key Lookup metrics. | `EXPLAIN ANALYZE` via pgAdmin or terminal. |
| **Filtered / Partial Indexes** | `HasFilter("[IsArchived] = 0")` natively supported in EF Core. | Partial indexes `WHERE is_archived = false`. |
| **Target Deployment Environment** | Seamless integration with Windows Server, IIS, and MonsterASP shared hosting. | Requires dedicated Linux VM or managed cloud PostgreSQL instance. |
| **Concurrency & Unique Constraints** | Deterministic error codes (`2601` / `2627`) for seat collision conflict resolution. | PostgreSQL error code `23505` (`unique_violation`). |

---

## ✅ Decision
We selected **Microsoft SQL Server** as the primary database engine.

### Key Drivers:
1. **Target Hosting Compatibility**: The production environment is deployed to MonsterASP shared hosting where SQL Server databases are natively provisioned and managed alongside the IIS application pool.
2. **Tooling & Deep Profiling**: SSMS and Azure Data Studio provide visual execution plans that simplify validating that filtered indexes convert table scans into index seeks.
3. **EF Core First-Party Alignment**: Clean migration generation, standard Identity tables, and robust transaction scope handling.

---

## 🚀 Consequences & Migration Considerations
* **Dialect Coupling**: Filtered index definitions use SQL Server syntax: `builder.HasIndex(s => s.IsArchived).HasFilter("[IsArchived] = 0")`.
* **Testing Infrastructure**: Integration tests in `Ticketa.Tests` leverage `Testcontainers.MsSql` to ensure tests execute against the exact engine behavior rather than relying on in-memory SQLite mocks that do not replicate SQL Server locking or index behaviors.
* **Future Migration Path**: The clean separation in `Ticketa.Infrastructure` ensures that if Ticketa transitions to Linux containers (Docker / Kubernetes), switching to PostgreSQL only requires updating the DbContext provider and migration scripts.
