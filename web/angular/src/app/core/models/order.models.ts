export type OrderStatus = 
  | 'Created'
  | 'Searching'
  | 'Assigned'
  | 'PickedUp'
  | 'Delivered'
  | 'Cancelled'
  | 'Failed';

export interface CreateOrderStopRequest {
  sequence: number;
  type: 'Pickup' | 'Dropoff';
  address: string;
  latitude: number;
  longitude: number;
  contactName?: string | null;
  contactPhone?: string | null;
}

export interface CreateOrderRequest {
  priority: number;
  vehicleType?: 'Bike' | 'Car' | 'Van' | 'Truck' | null;
  packageDescription?: string | null;
  packageWeight?: number | null;
  requestedPickupAt?: string | null;
  stops: CreateOrderStopRequest[];
}

export interface OrderStopResponse {
  id: string;
  sequence: number;
  type: string;
  address: string;
  latitude: number;
  longitude: number;
  contactName?: string | null;
  contactPhone?: string | null;
  completedAt?: string | null;
}

export interface OrderStatusHistoryResponse {
  id: number;
  fromStatus?: string | null;
  toStatus: string;
  reason?: string | null;
  actorType: string;
  occurredAt: string;
}

export interface AssignmentResponse {
  id: string;
  courierId: string;
  assignedAt: string;
  acceptedAt?: string | null;
  completedAt?: string | null;
  status: string;
}

export interface OrderResponse {
  id: string;
  orderNo: string;
  customerId: string;
  assignedCourierId?: string | null;
  status: OrderStatus;
  priority: number;
  vehicleType?: string | null;
  packageDescription?: string | null;
  packageWeight?: number | null;
  requestedPickupAt?: string | null;
  createdAt: string;
  updatedAt: string;
  stops: OrderStopResponse[];
  statusHistory: OrderStatusHistoryResponse[];
  activeAssignment?: AssignmentResponse | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface OrderFilterQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  customerId?: string;
  courierId?: string;
  fromDate?: string;
  toDate?: string;
}

export interface CancelOrderRequest {
  reason: string;
}
