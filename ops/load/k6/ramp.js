// Stage 5D.3 — Continuous concurrency ramp (10 -> 25 -> 50 -> 100 -> 250 -> 500) in a single k6
// process, as an alternative view to run-load.ps1's discrete per-level runs. The SLO thresholds in
// common.js (error rate > 1%, or p95 > 1s) make k6 exit non-zero once the system is past its knee,
// so the ramp itself encodes the stop condition rather than a human eyeballing the table.
import { userFor, runMixed, SLO } from './common.js';

export const options = Object.assign({}, SLO, {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 0,
      exec: 'default',
      stages: [
        { duration: '20s', target: 10 },
        { duration: '20s', target: 25 },
        { duration: '20s', target: 50 },
        { duration: '30s', target: 100 },
        { duration: '30s', target: 250 },
        { duration: '30s', target: 500 },
        { duration: '15s', target: 0 },
      ],
    },
  },
});

export default function () {
  runMixed(userFor(__VU));
}
