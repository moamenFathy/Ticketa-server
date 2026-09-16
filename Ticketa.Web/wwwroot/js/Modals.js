function openModal(formId, url, name) {
    fetch(url)
        .then(res => {
            if (!res.ok) {
                throw new Error(`Failed to load ${name} data.`);
            }

            return res.text();
        })
        .then(html => {
            document.getElementById("modalContent").innerHTML = html;
            const form = $(`#${formId}`);
            form.removeData("validator");
            form.removeData("unobtrusiveValidation");
            $.validator.unobtrusive.parse(form);
            // Open DaisyUI modal
            document.getElementById("modal").checked = true;

            // Focus autofocus elements manually after injection
            const autofocusInput = document.querySelector("#modalContent [autofocus]");
            if (autofocusInput) autofocusInput.focus();
        })
        .catch(() => {
            document.getElementById("modalContent").innerHTML = `<p class='text-error'>Could not load ${name} details.</p>
                                                                             <div class="modal-action">
                                                                                <label for="modal" class="btn btn-ghost">Cancel</label>
                                                                             </div>`;
            document.getElementById("modal").checked = true;
        });
}

document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
        const editModal = document.getElementById("modal");
        if (editModal) {
            editModal.checked = false;
        }
    }
});

document.addEventListener("click", function (e) {
    const modalWrapper = document.getElementsByClassName("modal-wrapper");
    const editModal = document.getElementById("modal");

    if (!modalWrapper || !editModal || !editModal.checked) {
        return;
    }

    if (e.target === modalWrapper) {
        editModal.checked = false;
    }
});

// One global listener handles all modals
document.addEventListener("submit", async (e) => {
    const form = e.target;

    // Each form declares its own url via data attribute
    const url = form.dataset.submitUrl;
    if (!url) return;

    e.preventDefault();

    // Check jQuery validation if it exists on the form
    if (typeof $(form).valid === "function" && !$(form).valid()) return;

    const submitBtn = form.querySelector("button[type='submit']");
    const originalBtnContent = submitBtn ? submitBtn.innerHTML : null;

    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="loading loading-spinner loading-sm"></span> Processing...';
    }

    await handleModalFormSubmit(form, url);

    if (submitBtn && originalBtnContent) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalBtnContent;
    }
});

async function handleModalFormSubmit(form, url) {
    const formData = new FormData(form);

    try {
        const response = await fetch(url, {
            method: 'POST',
            body: formData
        });

        const targetContainer = form.closest("#sheetContent") || document.getElementById("modalContent");

        // Validation failed or server error → re-render form with errors
        const contentType = response.headers.get("content-type");
        if (!response.ok || contentType?.includes("text/html")) {
            const html = await response.text();
            if (targetContainer) {
                targetContainer.innerHTML = html || "<p class='text-error'>Request failed.</p>";
            }

            const failedForm = $(`#${form.id}`);
            failedForm.removeData("validator").removeData("unobtrusiveValidation");
            $.validator.unobtrusive.parse(failedForm);
            return;
        }

        // Success → JSON
        const result = await response.json();
        if (result.success) {
            const modalCheckbox = document.getElementById("modal");
            if (modalCheckbox) modalCheckbox.checked = false;
            if (typeof window.closeRoleSheet === "function") {
                window.closeRoleSheet();
            }
            location.reload();
        } else if (result.message) {
            const errorBanner = document.getElementById("errorBanner");
            if (errorBanner) {
                errorBanner.innerText = result.message;
                errorBanner.classList.remove("hidden");
            } else {
                alert(result.message);
            }
        }

    } catch (error) {
        console.error("Submit error:", error);
    }
}