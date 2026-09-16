import { initDataTable } from "../DataTables.js";

const isSelf = (id) => id === window.currentUserId;

initDataTable("/AdminUserManagement/GetAll", [
  {
    data: "fullName",
    className: "align-middle font-semibold"
  },
  {
    data: "email",
    className: "align-middle"
  },
  {
    data: "role",
    className: "align-middle",
    render: (data) => {
      const isAdmin = data === "Admin";
      const colorClass = isAdmin
        ? "bg-purple-100 text-purple-800"
        : "bg-blue-100 text-blue-800";
      return `<div class="flex justify-center"><span class="badge badge-sm font-medium border-0 ${colorClass}">${data}</span></div>`;
    }
  },
  {
    data: "id",
    orderable: false,
    className: "align-middle text-center whitespace-nowrap",
    render: (id, _type, row) => {
      const deleteButton = isSelf(id)
        ? `<span class="text-xs text-base-content/40 italic">(you)</span>`
        : `<div class="tooltip" data-tip="Delete">
          <button type="button" class="btn btn-ghost btn-sm text-red-400 hover:bg-red-50" onclick="openModal('deleteForm', '/AdminUserManagement/DeleteConfirmation/${id}', 'user')">
            <svg xmlns="http://www.w3.org/2000/svg" height="16" width="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>`;

      return `
      <div class="flex flex-row justify-center items-center gap-2">
        <div class="tooltip" data-tip="Edit">
          <button type="button" class="btn btn-ghost btn-sm text-primary hover:bg-primary/10" onclick="openModal('userForm', '/AdminUserManagement/Upsert/${id}', 'user')">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z"/>
              <path d="m15 5 4 4"/>
            </svg>
          </button>
        </div>
        ${deleteButton}
      </div>`;
    }
  }
], {
  order: [[0, 'asc']],
  columnDefs: [
      { className: "flex justify-center gap-1", targets: 3 },
  ]
});
