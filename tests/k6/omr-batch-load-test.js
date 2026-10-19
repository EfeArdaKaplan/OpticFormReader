import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// =============================================================================
// OPTIMATRIX ENTERPRISE OMR - K6 LOAD & STRESS TESTING SUITE
// Tests high-throughput batch evaluation, progress polling, and results retrieval
// =============================================================================

// Custom Performance Metrics
const authDuration = new Trend('auth_duration_ms');
const evalDuration = new Trend('evaluation_duration_ms');
const successRate = new Rate('successful_evaluations');

// Configuration & SLA Thresholds
export const options = {
  scenarios: {
    // 1. Smoke Scenario: Verify endpoint stability with minimal load
    smoke: {
      executor: 'constant-vus',
      vus: 2,
      duration: '15s',
      startTime: '0s',
      tags: { test_type: 'smoke' },
    },
    // 2. Load Scenario: Simulate concurrent teachers/administrators evaluating exams
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 15 }, // Ramp up to 15 concurrent users
        { duration: '1m', target: 15 },  // Sustained load
        { duration: '15s', target: 0 },  // Graceful ramp down
      ],
      startTime: '20s',
      tags: { test_type: 'load' },
    },
  },
  thresholds: {
    // SLA Performance Budgets
    http_req_duration: ['p(95)<1500', 'p(99)<3000'], // 95% requests under 1.5s
    http_req_failed: ['rate<0.01'],                  // Error rate strictly below 1%
    successful_evaluations: ['rate>0.99'],           // Evaluation success rate above 99%
  },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:5144';
const USERNAME = __ENV.TEST_USER || 'admin';
const PASSWORD = __ENV.TEST_PASS || 'Admin123!';
const EXAM_ID = __ENV.EXAM_ID || '1';

// Setup: Authenticate once to obtain JWT token
export function setup() {
  const loginPayload = JSON.stringify({
    username: USERNAME,
    password: PASSWORD,
  });

  const res = http.post(`${BASE_URL}/api/Auth/login`, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(res, {
    'auth succeeded (200)': (r) => r.status === 200,
    'has token': (r) => r.json('token') !== undefined,
  });

  return { token: res.json('token') };
}

export default function (data) {
  const authHeaders = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${data.token}`,
  };

  // Step 1: Health check / Sınav Detayı
  const examRes = http.get(`${BASE_URL}/api/Exam/${EXAM_ID}`, { headers: authHeaders });
  check(examRes, {
    'get exam status 200 or 404': (r) => r.status === 200 || r.status === 404,
  });

  // Step 2: Sınav Değerlendirmesini Tetikle (Batch OMR Evaluation)
  const evalStart = Date.now();
  const evalRes = http.post(
    `${BASE_URL}/api/Exam/${EXAM_ID}/Evaluate`,
    null,
    { headers: authHeaders }
  );

  evalDuration.add(Date.now() - evalStart);

  const isEvalSuccessful = evalRes.status === 200 || evalRes.status === 400; // 400 if already empty/no sheets
  successRate.add(isEvalSuccessful);

  check(evalRes, {
    'evaluation completed without 500 error': (r) => r.status !== 500,
  });

  // Step 3: Anlık İlerleme Durumunu Sorgula (Progress Polling)
  const progressRes = http.get(`${BASE_URL}/api/Exam/${EXAM_ID}/Evaluate/progress`, {
    headers: authHeaders,
  });

  check(progressRes, {
    'progress poll status is 200': (r) => r.status === 200,
    'progress has percent field': (r) => r.json('percent') !== undefined,
  });

  // Step 4: Sıralama & Öğrenci Sonuçları (Rankings Query)
  const resultsRes = http.get(`${BASE_URL}/api/Result/Exam/${EXAM_ID}/Rankings`, {
    headers: authHeaders,
  });

  check(resultsRes, {
    'rankings retrieved status is 200': (r) => r.status === 200,
  });

  // Think time between evaluations
  sleep(1);
}
