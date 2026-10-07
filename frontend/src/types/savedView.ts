export type SavedViewType = "LIST" | "BOARD";

export interface SavedViewFilters {
  status: string[] | null;
  assigneeId: string | null;
  priority: string[] | null;
  labelId: number[] | null;
  keyword: string | null;
  sort: string | null;
}

export interface SavedView {
  id: number;
  workspaceId: number;
  name: string;
  viewType: SavedViewType;
  isShared: boolean;
  isOwner: boolean;
  filters: SavedViewFilters;
}

export interface SavedViewRequestBody {
  name: string;
  viewType: SavedViewType;
  isShared: boolean;
  filters: SavedViewFilters;
}
