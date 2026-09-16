import { initDataTable } from "../DataTables.js";

initDataTable("/Hall/GetAll", [
    { data: 'rowNumber' },
    { data: 'name' },
    {
        data: 'hallType',
        render: (data) => {
            let color = "bg-base-200 text-base-content";
            if (data === "Standard") color = "bg-blue-100 text-blue-800";
            else if (data === "IMAX") color = "bg-purple-100 text-purple-800";
            else if (data === "Gold") color = "bg-amber-100 text-amber-800";

            return `<div class="flex justify-center"><span class="badge badge-sm font-medium border-0 ${color}">${data}</span></div>`;
        }
    },
    {
        data: null,
        render: (data, type, row) => {
            return `
                <div class="flex flex-col items-center justify-center">
                    <span class="text-xs font-semibold">${row.totalRows} × ${row.seatsPerRow}</span>
                    <span class="text-[10px] text-base-content/50">${row.visibleSeatCount} bookable</span>
                </div>
            `;
        }
    },
    {
        data: 'id',
        orderable: false,
        render: (id, type, row) => `
                        <!-- View Map Action -->
                        <div class="tooltip" data-tip="View Map">
                             <button type="button" class="btn btn-ghost btn-sm text-blue-500 hover:bg-blue-50" onclick="openModal('viewMapForm', '/hall/ViewMap/${id}', 'seat map')">
                                 <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-grid-2x2">
                                    <rect width="18" height="18" x="3" y="3" rx="2"/><path d="M3 12h18"/><path d="M12 3v18"/>
                                 </svg>
                             </button>
                        </div>
                        <!-- Edit Action -->
                        <div class="tooltip" data-tip="Edit">
                             <button type="button" class="btn btn-ghost btn-sm text-primary hover:bg-primary/10" onclick="openModal('editForm', '/hall/Upsert/${id}', 'hall')">
                                 <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-pencil-icon lucide-pencil">
                                    <path d="M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z"/><path d="m15 5 4 4"/>
                                 </svg>
                             </button>
                        </div>
                        <!-- Delete Action -->
                        <div class="tooltip" data-tip="${row.hasShowtimes ? 'Cannot delete: Hall has active showtimes' : 'Delete'}">
                             <button type="button" 
                                     class="btn btn-ghost btn-sm text-red-400 hover:bg-red-50 ${row.hasShowtimes ? 'btn-disabled opacity-30' : ''}" 
                                     ${row.hasShowtimes ? 'disabled' : ''}
                                     onclick="openModal('deleteForm', '/hall/DeleteConfirmation/${id}', 'hall')">
                                        <svg xmlns="http://www.w3.org/2000/svg"
                                            height="16" width="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                  d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                             </button>
                        </div>
                    `
    }
], {
    order: [[0, 'asc']], // Ordered by ID asc makes more sense for halls (Hall 1, Hall 2...)
    columnDefs: [
        { className: "flex justify-center gap-1", targets: 4 } // Actions column is now index 4
    ]
});

// Handle Hall Type change in the Create Modal
document.addEventListener('change', function(e) {
    if (e.target && e.target.id === 'hallTypeSelect') {
        const templates = {
            '0': { rows: 12, seats: 16, categories: 'Rows 1–9: Regular | Rows 10–12: VIP' },          // Standard
            '1': { rows: 14, seats: 16, categories: 'Rows 1–10: Regular | Rows 11–14: Premium' },     // IMAX
            '2': { rows: 6,  seats: 8,  categories: 'All rows: Regular' }                             // Gold
        };
        const getInvisibleCount = (rows) => {
            let count = 0;
            for (let r = 0; r < rows; r++) {
                const skip = r === 0 ? 3 : r === rows - 1 ? 2 : 0;
                count += skip * 2;
            }
            return count;
        };
        const t = templates[e.target.value];
        if(t) {
            const total = t.rows * t.seats;
            const invisible = getInvisibleCount(t.rows);
            document.getElementById('previewRows').textContent   = t.rows;
            document.getElementById('previewSeats').textContent  = t.seats;
            document.getElementById('previewTotal').textContent  = total - invisible;
            document.getElementById('previewCategories').textContent = t.categories;
        }
    }
});