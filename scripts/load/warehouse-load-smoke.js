import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 20,
  duration: '1m',
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000']
  }
};

const baseUrl = __ENV.BASE_URL || 'http://localhost:5000';
const token = __ENV.TOKEN || '';

function authHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

export default function () {
  const health = http.get(`${baseUrl}/health`);
  check(health, { 'health is 2xx': (r) => r.status >= 200 && r.status < 300 });

  const waves = http.get(`${baseUrl}/api/warehouse/v1/waves`, { headers: authHeaders() });
  check(waves, { 'waves endpoint responds': (r) => [200, 401, 403].includes(r.status) });

  const analytics = http.get(`${baseUrl}/api/warehouse/v1/analytics/fulfillment-kpis`, { headers: authHeaders() });
  check(analytics, { 'analytics endpoint responds': (r) => [200, 401, 403].includes(r.status) });

  sleep(1);
}
