export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phone?: string;
  role: 'Customer' | 'Courier';
  vehicleType?: 'Bike' | 'Car' | 'Van' | 'Truck';
  plateNumber?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  user: UserProfile;
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  phone?: string | null;
  status: string;
  roles: string[];
  createdAt: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface WebSocketTicketResponse {
  ticket: string;
  expiresIn: number;
  expiresAt: string;
}
