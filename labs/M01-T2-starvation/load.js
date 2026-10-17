// M01-T2 load test: 100 virtual users for 30 s against one endpoint (closed model, as the task asks).
//   k6 run -e ENDPOINT=slow-sync  labs/M01-T2-starvation/load.js
//   k6 run -e ENDPOINT=slow-async labs/M01-T2-starvation/load.js
// Read from the summary: http_reqs (count and rate = RPS), http_req_duration p(95), http_req_failed.
import http from 'k6/http';

const endpoint = __ENV.ENDPOINT || 'slow-sync';

export const options = {
  vus: 100,
  duration: '30s',
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'max'],
};

export default function () {
  http.get(`http://localhost:5000/${endpoint}`);
}
