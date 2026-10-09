import { Component, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterOutlet } from '@angular/router';

interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected readonly title = signal('logisticclient');
  protected readonly serverStatus = signal<'checking' | 'connected' | 'error'>('checking');
  protected readonly forecasts = signal<WeatherForecast[]>([]);
  protected readonly errorMessage = signal<string>('');

  private readonly http = inject(HttpClient);

  ngOnInit(): void {
    this.checkServerConnection();
  }

  checkServerConnection(): void {
    this.serverStatus.set('checking');
    this.errorMessage.set('');

    // Try proxy first (/weatherforecast), fallback to direct backend url if running outside proxy
    this.http.get<WeatherForecast[]>('/weatherforecast').subscribe({
      next: (data) => {
        this.serverStatus.set('connected');
        this.forecasts.set(data);
      },
      error: (err) => {
        // Fallback to direct backend URL for standalone dev
        this.http.get<WeatherForecast[]>('http://localhost:5229/weatherforecast').subscribe({
          next: (data) => {
            this.serverStatus.set('connected');
            this.forecasts.set(data);
          },
          error: (fallbackErr) => {
            this.serverStatus.set('error');
            this.errorMessage.set(fallbackErr.message || 'Unable to reach LogisticServer API');
          }
        });
      }
    });
  }
}
