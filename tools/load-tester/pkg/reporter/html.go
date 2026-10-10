package reporter

import (
	"encoding/json"
	"fmt"
	"os"
	"time"

	"load-tester/pkg/metrics"
	"load-tester/pkg/monitor"
)

// GenerateHTMLReport creates a self-contained HTML report with interactive Chart.js charts.
func GenerateHTMLReport(filePath string, report metrics.SummaryReport, watchdog *monitor.Watchdog) error {
	stepsJSON, _ := json.Marshal(report.Steps)
	endpointsJSON, _ := json.Marshal(report.EndpointStats)

	servicesList := []monitor.ServiceHealth{}
	if watchdog != nil {
		servicesList = watchdog.GetServices()
	}
	servicesJSON, _ := json.Marshal(servicesList)

	breakingPointText := "None Detected (Fully Stable)"
	if report.BreakingPointVU > 0 {
		breakingPointText = fmt.Sprintf("~%d Concurrent Users", report.BreakingPointVU)
	}

	htmlContent := fmt.Sprintf(`<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Load & Stress Test Report - Distributed Logistics</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <style>
    :root {
      --bg-base: #0a0f1d;
      --bg-surface: #11192e;
      --bg-elevated: #17233f;
      --border: #1e293b;
      --text: #f8fafc;
      --text-muted: #94a3b8;
      --accent: #06b6d4;
      --green: #10b981;
      --amber: #f59e0b;
      --red: #ef4444;
    }
    body {
      margin: 0;
      padding: 24px;
      background: var(--bg-base);
      color: var(--text);
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .container { max-width: 1200px; margin: 0 auto; }
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid var(--border);
      padding-bottom: 20px;
      margin-bottom: 24px;
    }
    h1 { margin: 0; font-size: 24px; font-weight: 700; color: var(--text); }
    .subtitle { color: var(--text-muted); font-size: 14px; margin-top: 4px; }
    .badge {
      padding: 6px 12px;
      border-radius: 9999px;
      font-size: 12px;
      font-weight: 600;
      background: rgba(6, 182, 212, 0.15);
      color: var(--accent);
      border: 1px solid rgba(6, 182, 212, 0.3);
    }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .card {
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 18px;
    }
    .card-label { font-size: 12px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.05em; }
    .card-value { font-size: 26px; font-weight: 700; margin-top: 8px; font-family: monospace; }
    .chart-container {
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 20px;
      margin-bottom: 24px;
    }
    .chart-title { font-size: 16px; font-weight: 600; margin-bottom: 16px; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    th { padding: 12px; background: var(--bg-elevated); color: var(--text-muted); font-weight: 600; border-bottom: 1px solid var(--border); }
    td { padding: 12px; border-bottom: 1px solid var(--border); font-family: monospace; }
    .diag-box {
      background: rgba(6, 182, 212, 0.08);
      border: 1px solid rgba(6, 182, 212, 0.3);
      border-radius: 12px;
      padding: 18px;
      margin-bottom: 24px;
      font-size: 15px;
      line-height: 1.5;
    }
    .status-ok { color: var(--green); }
    .status-warn { color: var(--amber); }
    .status-err { color: var(--red); }
  </style>
</head>
<body>
<div class="container">
  <div class="header">
    <div>
      <h1>Distributed Logistics Platform - Stress & Load Benchmark</h1>
      <div class="subtitle">Target: <b>%s</b> | Mode: <b>%s</b> | Generated: %s</div>
    </div>
    <div class="badge">%s</div>
  </div>

  <div class="diag-box">
    <b>System Capacity Assessment:</b><br/>
    %s
  </div>

  <div class="grid">
    <div class="card">
      <div class="card-label">Total Requests</div>
      <div class="card-value">%d</div>
    </div>
    <div class="card">
      <div class="card-label">Success Rate</div>
      <div class="card-value status-ok">%.2f%%</div>
    </div>
    <div class="card">
      <div class="card-label">Peak Throughput</div>
      <div class="card-value">%.1f <span style="font-size:14px;color:var(--text-muted)">RPS</span></div>
    </div>
    <div class="card">
      <div class="card-label">Breaking Point</div>
      <div class="card-value status-warn">%s</div>
    </div>
    <div class="card">
      <div class="card-label">P95 Latency</div>
      <div class="card-value">%v</div>
    </div>
  </div>

  <div class="chart-container">
    <div class="chart-title">Throughput (RPS) & Error Rate vs Concurrent Users</div>
    <canvas id="rampChart" height="90"></canvas>
  </div>

  <div class="chart-container">
    <div class="chart-title">Latency Percentiles Progression (P50, P90, P95, P99)</div>
    <canvas id="latencyChart" height="90"></canvas>
  </div>

  <div class="chart-container">
    <div class="chart-title">Microservice Health Matrix & Availability</div>
    <table>
      <thead>
        <tr>
          <th>Service Name</th>
          <th>Endpoint URL</th>
          <th>Health Status</th>
          <th>Last Response Time</th>
          <th>Failure Point (VU)</th>
        </tr>
      </thead>
      <tbody id="servicesTable"></tbody>
    </table>
  </div>

  <div class="chart-container">
    <div class="chart-title">Endpoint Performance Breakdown</div>
    <table>
      <thead>
        <tr>
          <th>Method</th>
          <th>Endpoint</th>
          <th>Requests</th>
          <th>Errors</th>
          <th>Avg Latency</th>
          <th>P95 Latency</th>
          <th>P99 Latency</th>
        </tr>
      </thead>
      <tbody id="endpointsTable"></tbody>
    </table>
  </div>
</div>

<script>
  const steps = %s;
  const services = %s;
  const endpoints = %s;

  // 1. Ramp Chart
  if (steps && steps.length > 0) {
    const labels = steps.map(s => s.vus + ' VUs');
    const rpsData = steps.map(s => s.rps);
    const errData = steps.map(s => s.errorRate);

    new Chart(document.getElementById('rampChart'), {
      type: 'line',
      data: {
        labels: labels,
        datasets: [
          {
            label: 'Throughput (RPS)',
            data: rpsData,
            borderColor: '#06b6d4',
            backgroundColor: 'rgba(6, 182, 212, 0.1)',
            yAxisID: 'y',
            tension: 0.3
          },
          {
            label: 'Error Rate (%%)',
            data: errData,
            borderColor: '#ef4444',
            backgroundColor: 'rgba(239, 68, 68, 0.1)',
            yAxisID: 'y1',
            tension: 0.3
          }
        ]
      },
      options: {
        responsive: true,
        scales: {
          y: { type: 'linear', position: 'left', grid: { color: '#1e293b' } },
          y1: { type: 'linear', position: 'right', grid: { drawOnChartArea: false }, min: 0, max: 100 }
        }
      }
    });

    // 2. Latency Chart
    const p50Data = steps.map(s => s.p50 / 1000000); // ms
    const p90Data = steps.map(s => s.p90 / 1000000);
    const p95Data = steps.map(s => s.p95 / 1000000);
    const p99Data = steps.map(s => s.p99 / 1000000);

    new Chart(document.getElementById('latencyChart'), {
      type: 'line',
      data: {
        labels: labels,
        datasets: [
          { label: 'P50 (ms)', data: p50Data, borderColor: '#10b981', tension: 0.2 },
          { label: 'P90 (ms)', data: p90Data, borderColor: '#3b82f6', tension: 0.2 },
          { label: 'P95 (ms)', data: p95Data, borderColor: '#f59e0b', tension: 0.2 },
          { label: 'P99 (ms)', data: p99Data, borderColor: '#ef4444', tension: 0.2 }
        ]
      },
      options: {
        responsive: true,
        scales: {
          y: { grid: { color: '#1e293b' }, title: { display: true, text: 'Milliseconds' } }
        }
      }
    });
  }

  // Populate Services Table
  const svcTable = document.getElementById('servicesTable');
  services.forEach(s => {
    const tr = document.createElement('tr');
    const statusClass = s.status === 'HEALTHY' ? 'status-ok' : (s.status === 'DEGRADED' ? 'status-warn' : 'status-err');
    const failedAt = s.failedAtVu ? s.failedAtVu + ' VUs' : 'N/A';
    const latMs = (s.lastLatency / 1000000).toFixed(1) + ' ms';
    tr.innerHTML = '<td><b>' + s.name + '</b></td><td>' + s.url + '</td><td class="' + statusClass + '">● ' + s.status + '</td><td>' + latMs + '</td><td>' + failedAt + '</td>';
    svcTable.appendChild(tr);
  });

  // Populate Endpoints Table
  const epTable = document.getElementById('endpointsTable');
  Object.keys(endpoints).forEach(k => {
    const ep = endpoints[k];
    const tr = document.createElement('tr');
    tr.innerHTML = '<td><span class="badge">' + ep.method + '</span></td><td>' + ep.endpoint + '</td><td>' + ep.totalRequests + '</td><td>' + ep.errorCount + '</td><td>' + (ep.avgDuration/1000000).toFixed(1) + ' ms</td><td>' + (ep.p95Duration/1000000).toFixed(1) + ' ms</td><td>' + (ep.p99Duration/1000000).toFixed(1) + ' ms</td>';
    epTable.appendChild(tr);
  });
</script>
</body>
</html>`,
		report.TargetURL,
		report.Mode,
		report.EndTime.Format("2006-01-02 15:04:05"),
		report.Mode,
		report.Diagnosis,
		report.TotalRequests,
		100.0-report.ErrorRate,
		report.PeakRPS,
		breakingPointText,
		report.P95Latency.Round(time.Millisecond),
		string(stepsJSON),
		string(servicesJSON),
		string(endpointsJSON),
	)

	return os.WriteFile(filePath, []byte(htmlContent), 0644)
}

