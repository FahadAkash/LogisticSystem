import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CourierDetailResponse,
  CourierFilterQuery,
  UpdateCourierStatusRequest,
} from '../models/courier.models';
import { PagedResult } from '../models/order.models';

@Injectable({
  providedIn: 'root',
})
export class CourierService {
  private readonly http = inject(HttpClient);

  getCouriers(query?: CourierFilterQuery): Observable<PagedResult<CourierDetailResponse>> {
    let params = new HttpParams();
    if (query?.page) params = params.set('page', query.page.toString());
    if (query?.pageSize) params = params.set('pageSize', query.pageSize.toString());
    if (query?.status) params = params.set('status', query.status);
    if (query?.vehicleType) params = params.set('vehicleType', query.vehicleType);

    return this.http.get<PagedResult<CourierDetailResponse>>('/api/couriers', { params });
  }

  getCourierById(id: string): Observable<CourierDetailResponse> {
    return this.http.get<CourierDetailResponse>(`/api/couriers/${id}`);
  }

  getMe(): Observable<CourierDetailResponse> {
    return this.http.get<CourierDetailResponse>('/api/couriers/me');
  }

  updateStatus(id: string, status: 'Available' | 'Offline' | 'Busy'): Observable<CourierDetailResponse> {
    const payload: UpdateCourierStatusRequest = { status };
    return this.http.put<CourierDetailResponse>(`/api/couriers/${id}/status`, payload);
  }

  getPendingCouriers(): Observable<CourierDetailResponse[]> {
    return this.http.get<CourierDetailResponse[]>('/api/admin/couriers/pending');
  }

  approveCourier(id: string): Observable<CourierDetailResponse> {
    return this.http.post<CourierDetailResponse>(`/api/admin/couriers/${id}/approve`, {});
  }
}
