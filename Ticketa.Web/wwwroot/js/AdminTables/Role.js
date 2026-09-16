import { initDataTable } from "../DataTables.js";

// Live permission counts and group select-all synchronization
function updatePermissionCounts() {
  const contentEl = document.getElementById("sheetContent");
  if (!contentEl) return;

  const allChecks = contentEl.querySelectorAll(".perm-check");
  const checkedChecks = contentEl.querySelectorAll(".perm-check:checked");
  const totalCount = allChecks.length;
  const checkedCount = checkedChecks.length;

  const counterBadge = document.getElementById("permCounter");
  if (counterBadge) {
    counterBadge.textContent = `${checkedCount} / ${totalCount} Selected`;
  }

  // Update each group counter and group select-all checkbox
  const groupCounters = contentEl.querySelectorAll(".group-counter");
  groupCounters.forEach(function (gc) {
    const groupClass = gc.getAttribute("data-group");
    const groupChecks = contentEl.querySelectorAll(".perm-check." + groupClass);
    const groupChecked = contentEl.querySelectorAll(".perm-check." + groupClass + ":checked");
    gc.textContent = `${groupChecked.length}/${groupChecks.length}`;

    const selectAllCb = contentEl.querySelector(`.select-all[data-group="${groupClass}"]`);
    if (selectAllCb) {
      selectAllCb.checked = groupChecks.length > 0 && groupChecked.length === groupChecks.length;
    }
  });
}

// Open slide-over sheet
window.openRoleSheet = async function (url) {
  const sheetEl = document.getElementById("roleSheet");
  const contentEl = document.getElementById("sheetContent");
  if (!sheetEl || !contentEl) return;

  contentEl.innerHTML = `
    <div class="flex flex-col items-center justify-center h-full p-8 text-base-content/60 space-y-3">
      <span class="loading loading-spinner loading-lg text-primary"></span>
      <p class="text-sm font-medium animate-pulse">Loading role details...</p>
    </div>`;

  sheetEl.classList.add("is-open");
  document.body.classList.add("overflow-hidden");

  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error("Failed to load role details.");
    const html = await res.text();
    contentEl.innerHTML = html;

    // Initialize jQuery unobtrusive validation
    const form = $("#roleForm");
    if (form.length && typeof $.validator !== "undefined") {
      form.removeData("validator");
      form.removeData("unobtrusiveValidation");
      $.validator.unobtrusive.parse(form);
    }

    // Auto-focus role name input
    const autofocusEl = contentEl.querySelector("[autofocus]");
    if (autofocusEl) autofocusEl.focus();

    updatePermissionCounts();
  } catch (err) {
    contentEl.innerHTML = `
      <div class="flex flex-col items-center justify-center h-full p-8 text-center space-y-4">
        <div class="size-12 rounded-full bg-error/10 text-error flex items-center justify-center">
          <svg xmlns="http://www.w3.org/2000/svg" class="size-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>
        </div>
        <div>
          <h4 class="font-bold text-base">Failed to Load Role</h4>
          <p class="text-xs text-base-content/60 mt-1">${err.message || "An unexpected error occurred."}</p>
        </div>
        <button type="button" class="btn btn-sm btn-ghost border border-base-300" onclick="closeRoleSheet()">Close</button>
      </div>`;
  }
};

// Close slide-over sheet
window.closeRoleSheet = function () {
  const sheetEl = document.getElementById("roleSheet");
  if (sheetEl) {
    sheetEl.classList.remove("is-open");
  }
  document.body.classList.remove("overflow-hidden");
};

// Escape key listener for slide-over sheet
document.addEventListener("keydown", function (e) {
  if (e.key === "Escape") {
    const sheetEl = document.getElementById("roleSheet");
    if (sheetEl && sheetEl.classList.contains("is-open")) {
      window.closeRoleSheet();
    }
  }
});

// Global click & change delegation for role sheet actions
document.addEventListener("click", function (e) {
  if (e.target.closest("#btnSelectAllPerms")) {
    const contentEl = document.getElementById("sheetContent");
    if (!contentEl) return;
    const checks = contentEl.querySelectorAll(".perm-check");
    checks.forEach(function (cb) { cb.checked = true; });
    const selectAlls = contentEl.querySelectorAll(".select-all");
    selectAlls.forEach(function (sa) { sa.checked = true; });
    updatePermissionCounts();
    return;
  }

  if (e.target.closest("#btnDeselectAllPerms")) {
    const contentEl = document.getElementById("sheetContent");
    if (!contentEl) return;
    const checks = contentEl.querySelectorAll(".perm-check");
    checks.forEach(function (cb) { cb.checked = false; });
    const selectAlls = contentEl.querySelectorAll(".select-all");
    selectAlls.forEach(function (sa) { sa.checked = false; });
    updatePermissionCounts();
    return;
  }
});

document.addEventListener("change", function (e) {
  const contentEl = document.getElementById("sheetContent");
  if (!contentEl) return;

  const selectAll = e.target.closest(".select-all");
  if (selectAll) {
    const group = selectAll.getAttribute("data-group");
    const checks = contentEl.querySelectorAll(".perm-check." + group);
    checks.forEach(function (cb) { cb.checked = selectAll.checked; });
    updatePermissionCounts();
    return;
  }

  const permCheck = e.target.closest(".perm-check");
  if (permCheck) {
    updatePermissionCounts();
  }
});

// DataTable initialization
initDataTable("/Role/GetAll", [
  {
    data: "name",
    className: "align-middle font-semibold"
  },
  {
    data: "isAdminRole",
    className: "align-middle",
    render: (data) => {
      if (data) {
        return `<div class="flex justify-center"><span class="badge badge-sm font-medium border-0 bg-primary/15 text-primary">Admin</span></div>`;
      }
      return `<div class="flex justify-center"><span class="badge badge-sm font-medium border-0 bg-base-200 text-base-content/60">Standard</span></div>`;
    }
  },
  {
    data: "permissionCount",
    className: "align-middle",
    render: (data) => {
      return `<div class="flex justify-center"><span class="badge badge-sm font-semibold border border-primary/30 bg-primary/10 text-primary">${data}</span></div>`;
    }
  },
  {
    data: "userCount",
    className: "align-middle"
  },
  {
    data: "id",
    orderable: false,
    className: "align-middle text-center whitespace-nowrap",
    render: (id, _type, row) => `
      <div class="flex flex-row justify-center items-center gap-2">
        <div class="tooltip" data-tip="Edit">
          <button type="button" class="btn btn-ghost btn-sm text-primary hover:bg-primary/10" onclick="openRoleSheet('/Role/LoadForEdit/${id}')">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z"/>
              <path d="m15 5 4 4"/>
            </svg>
          </button>
        </div>
        <div class="tooltip" data-tip="${row.userCount > 0 ? 'Cannot delete: role has assigned users' : 'Delete'}">
          <button type="button"
                  class="btn btn-ghost btn-sm text-red-400 hover:bg-red-50 ${row.userCount > 0 ? 'btn-disabled opacity-30' : ''}"
                  ${row.userCount > 0 ? 'disabled' : ''}
                  onclick="openModal('deleteForm', '/Role/DeleteConfirmation/${id}', 'role')">
            <svg xmlns="http://www.w3.org/2000/svg" height="16" width="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </div>`
  }
], {
  order: [[0, 'asc']],
  columnDefs: [
    { className: "flex justify-center gap-1", targets: 4 }
  ]
});
