# Mudaac object visibility

Select **Mudaac Model** and use its **Mudaac Object Manager** component.

- Each checkbox enables or disables the objects in that semantic group.
- **Isolate** shows only that group. Check additional groups to combine them.
- **All tiles** toggles all ten tile sections; its Isolate button hides everything else in the model.
- **Show All** and **Hide All** operate on all discovered model groups.
- **Restore Previous Visibility** restores the exact active states from before the first isolate or bulk action, including objects that were already hidden. Repeated isolates retain the same restore point.
- Expand a group to inspect its object references. **Refresh Object Groups** discovers added/renamed objects, including inactive ones, without changing their visibility.

The model has 21 groups: ten tile sections, sculpture and pedestal, glass entrance and frame, historic building left of the entrance, other facade glazing, museum signs/plaques, roof railings, trees/plants, garden supports, garden ground/paths, flagpoles and sculpture terrace. Unknown renderer objects appear in **Other objects**.

Visibility toggles change GameObject activation and support Undo/Redo. Saved scenes retain the chosen visibility and restore point. Initial discovery preserves the current setup, including hidden garden objects. Existing model containers are reused without reparenting. The manager and cameras/lights outside **Model Root** stay active during isolation.

Tile visibility is independent of inclusion in `MudaacTileController`: excluded modifier groups, such as the undercroft, can still be shown or hidden here. Original transforms and materials are not modified by visibility controls.

Runtime/automation entry points: `SetGroupVisible(id, bool)`, `SetTilesVisible(bool)`, `IsolateGroup(id)`, `IsolateTiles()`, `ShowAll()`, `HideAll()`, `RestoreVisibility()` and `RefreshGroups()`. Inspect `Groups` for stable IDs, display names and object lists. IDs include `sculpture`, `glass-entrance`, `historic-building`, `vegetation`, and `tiles:LeftCantilever`.
