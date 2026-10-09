import {
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  ChangeDetectionStrategy,
  input,
  effect,
} from '@angular/core';
import * as L from 'leaflet';
import { CommonModule } from '@angular/common';

export interface MapMarker {
  id: string;
  latitude: number;
  longitude: number;
  title: string;
  subtitle?: string;
  type: 'courier' | 'pickup' | 'dropoff';
  status?: string;
}

@Component({
  selector: 'app-live-map',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="map-wrapper">
      <div #mapContainer class="map-container"></div>
      @if (markers().length === 0) {
        <div class="map-empty-overlay">
          <span>Awaiting coordinates & telemetry stream...</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .map-wrapper {
      position: relative;
      width: 100%;
      height: 100%;
      min-height: 380px;
      border-radius: 0.75rem;
      overflow: hidden;
      border: 1px solid #1e293b;
      background: #0f172a;
    }
    .map-container {
      width: 100%;
      height: 100%;
      min-height: 380px;
    }
    .map-empty-overlay {
      position: absolute;
      top: 1rem;
      left: 1rem;
      background: rgba(15, 23, 42, 0.85);
      backdrop-filter: blur(4px);
      padding: 0.4rem 0.8rem;
      border-radius: 0.5rem;
      border: 1px solid #334155;
      color: #94a3b8;
      font-size: 0.75rem;
      z-index: 1000;
      pointer-events: none;
    }
    :host ::ng-deep .custom-pin {
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      color: #ffffff;
      font-weight: 700;
      font-size: 11px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.4);
      border: 2px solid #ffffff;
    }
    :host ::ng-deep .pin-courier {
      background: #10b981;
      width: 26px;
      height: 26px;
    }
    :host ::ng-deep .pin-pickup {
      background: #f59e0b;
      width: 28px;
      height: 28px;
    }
    :host ::ng-deep .pin-dropoff {
      background: #3b82f6;
      width: 28px;
      height: 28px;
    }
  `],
})
export class LiveMapComponent implements OnInit, OnDestroy {
  @ViewChild('mapContainer', { static: true }) mapElement!: ElementRef<HTMLDivElement>;

  readonly markers = input<MapMarker[]>([]);
  readonly polylineCoords = input<[number, number][]>([]);

  private map: L.Map | null = null;
  private markerLayer = L.layerGroup();
  private routeLine: L.Polyline | null = null;

  constructor() {
    // React to markers and polyline updates reactively
    effect(() => {
      const currentMarkers = this.markers();
      const currentCoords = this.polylineCoords();
      if (this.map) {
        this.updateMapLayers(currentMarkers, currentCoords);
      }
    });
  }

  ngOnInit(): void {
    this.initMap();
  }

  private initMap(): void {
    if (this.map) return;

    // Default center New York
    this.map = L.map(this.mapElement.nativeElement, {
      center: [40.7128, -74.0060],
      zoom: 13,
      zoomControl: true,
    });

    // Dark cartographic tiles for dark operations console
    L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
      attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
      maxZoom: 19,
    }).addTo(this.map);

    this.markerLayer.addTo(this.map);
    this.updateMapLayers(this.markers(), this.polylineCoords());
  }

  private updateMapLayers(markers: MapMarker[], coords: [number, number][]): void {
    if (!this.map) return;

    this.markerLayer.clearLayers();

    const bounds = L.latLngBounds([]);

    for (const m of markers) {
      if (isNaN(m.latitude) || isNaN(m.longitude)) continue;

      const latLng = L.latLng(m.latitude, m.longitude);
      bounds.extend(latLng);

      const iconClass = `custom-pin pin-${m.type}`;
      const iconLabel = m.type === 'courier' ? '🛵' : (m.type === 'pickup' ? 'P' : 'D');

      const customIcon = L.divIcon({
        className: iconClass,
        html: `<span>${iconLabel}</span>`,
        iconSize: [28, 28],
        iconAnchor: [14, 14],
      });

      const marker = L.marker(latLng, { icon: customIcon });
      marker.bindPopup(`
        <div style="font-family: inherit; font-size: 12px; color: #0f172a;">
          <strong>${m.title}</strong>
          ${m.subtitle ? `<div style="color: #64748b;">${m.subtitle}</div>` : ''}
          ${m.status ? `<div style="margin-top: 4px; font-weight: 600;">Status: ${m.status}</div>` : ''}
        </div>
      `);
      this.markerLayer.addLayer(marker);
    }

    // Draw route polyline if coordinates available
    if (this.routeLine) {
      this.map.removeLayer(this.routeLine);
      this.routeLine = null;
    }

    if (coords && coords.length > 1) {
      this.routeLine = L.polyline(coords, {
        color: '#3b82f6',
        weight: 3,
        dashArray: '6, 8',
        opacity: 0.8,
      }).addTo(this.map);

      for (const c of coords) {
        bounds.extend(L.latLng(c[0], c[1]));
      }
    }

    if (bounds.isValid() && markers.length > 0) {
      this.map.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
    }
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }
}
