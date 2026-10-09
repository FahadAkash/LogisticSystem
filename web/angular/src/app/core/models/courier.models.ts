export type CourierStatus = 
  | 'PendingApproval'
  | 'Offline'
  | 'Available'
  | 'Busy'
  | 'Suspended';

export interface VehicleDetail {
  id: string;
  type: 'Bike' | 'Car' | 'Van' | 'Truck';
  plateNumber: string;
  capacityKg?: number | null;
}

export interface CourierDetailResponse {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  status: CourierStatus;
  rating: number;
  vehicle?: VehicleDetail | null;
  activeOrder?: {
    id: string;
    orderNo: string;
    status: string;
  } | null;
  createdAt: string;
  updatedAt: string;
}

export interface CourierFilterQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  vehicleType?: string;
}

export interface UpdateCourierStatusRequest {
  status: 'Available' | 'Offline' | 'Busy';
}

