import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CancelOrderRequest,
  CreateOrderRequest,
  OrderFilterQuery,
  OrderResponse,
  PagedResult,
} from '../models/order.models';

@Injectable({
  providedIn: 'root',
})
export class OrderService {
  private readonly http = inject(HttpClient);

  createOrder(request: CreateOrderRequest, idempotencyKey?: string): Observable<OrderResponse> {
    const key = idempotencyKey || this.generateUUID();
    return this.http.post<OrderResponse>('/api/orders', request, {
      headers: {
        'Idempotency-Key': key,
      },
    });
  }

  getOrders(query?: OrderFilterQuery): Observable<PagedResult<OrderResponse>> {
    let params = new HttpParams();
    if (query?.page) params = params.set('page', query.page.toString());
    if (query?.pageSize) params = params.set('pageSize', query.pageSize.toString());
    if (query?.status) params = params.set('status', query.status);
    if (query?.customerId) params = params.set('customerId', query.customerId);
    if (query?.courierId) params = params.set('courierId', query.courierId);
    if (query?.fromDate) params = params.set('fromDate', query.fromDate);
    if (query?.toDate) params = params.set('toDate', query.toDate);

    return this.http.get<PagedResult<OrderResponse>>('/api/orders', { params });
  }

  getOrderById(id: string): Observable<OrderResponse> {
    return this.http.get<OrderResponse>(`/api/orders/${id}`);
  }

  cancelOrder(id: string, reason: string): Observable<OrderResponse> {
    const payload: CancelOrderRequest = { reason };
    return this.http.post<OrderResponse>(`/api/orders/${id}/cancel`, payload);
  }

  pickupOrder(id: string): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(`/api/orders/${id}/pickup`, {});
  }

  deliverOrder(id: string): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(`/api/orders/${id}/deliver`, {});
  }

  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}

