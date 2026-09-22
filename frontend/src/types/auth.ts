export interface LoginRequest {
  email: string;
  password: string;
}

export interface CurrentUser {
  id: number;
  name: string;
  email: string;
}

export interface LoginResponse {
  accessToken: string;
  user: CurrentUser;
}
