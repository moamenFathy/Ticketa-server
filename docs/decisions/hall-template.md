# 🏛️ ADR 002: Fixed Mathematical Hall Templates

## 📌 Status
**Accepted**

---

## 🎯 Context
Cinema theaters require structured seat layouts for booking and ticket allocation. We evaluated two architectural approaches for managing hall layouts:

1. **Option A (Manual Grid Editor)**: An admin draws custom seat grids per hall, toggling existence, categories, and aisles manually. Every seat is stored as a persistent row in a `Seat` database table.
2. **Option B (Fixed Mathematical Templates)**: Layouts are pre-configured per `HallType` (`Standard`, `IMAX`, `Gold`). The seat structure is calculated mathematically on demand via `HallTypeHelper`, and no static `Seat` table exists in the database.

---

## ⚖️ Evaluation & Comparison

| Aspect | Option A: Manual Grid Editor | Option B: Fixed Templates (Chosen) |
| :--- | :--- | :--- |
| **Database Footprint** | Thousands of static `Seat` rows per cinema complex. | **Zero `Seat` rows**. Only active bookings store `BookedSeat` records. |
| **Development Cost** | 2–3 weeks building a complex drag-and-drop / bulk-assign seat editor. | **1–2 days** implementing mathematical helpers. |
| **Real-World Alignment** | Custom auditoriums only. | Matches major cinema chains (VOX Cinemas, AMC) using standardized hall types. |
| **Maintenance Overhead** | High schema complexity, cascading joins, fragmentation. | Pure functions in `Core`, minimal DB schema, zero seat table fragmentation. |

---

## ✅ Decision
We selected **Option B: Fixed Mathematical Template per Hall Type**.

### Architecture Implementation:
* **`HallTypeHelper.cs` in `Ticketa.Core`**: Single source of truth defining rows, seats per row, category mappings, and stadium bowl skips:
  * **Standard**: 10 rows $\times$ 12 seats (Rows 1–8: Regular, Rows 9–10: VIP).
  * **IMAX**: 14 rows $\times$ 16 seats (Rows 1–10: Regular, Rows 11–14: Premium).
  * **Gold**: 6 rows $\times$ 8 seats (Rows 1–3: GoldLounge, Rows 4–6: GoldRecliner).
* **On-Demand Layout Generation**: When an admin creates a `Hall`, `TotalRows` and `SeatsPerRow` are cached on the entity for quick display, while the seat grid is synthesized dynamically during booking sessions.

---

## 🚀 Consequences
* **Simpler Migrations**: No `Seat` table migrations or foreign key cascades required.
* **Separation of Concerns**: Stadium bowl curvature skips are calculated in backend domain helpers (`GetSkip`), while central walking aisles are treated as UI presentation concerns in the React client.
* **Future Extension Path**: If a unique hall requires custom adjustments in the future, an optional `OverrideLayout` JSON column can be added to the `Hall` entity without requiring architectural changes.
