export interface Label {
  id: number;
  workspaceId: number;
  name: string;
  color: string | null;
}

export interface LabelRequestBody {
  name: string;
  color?: string | null;
}
