// Stage 5D.2 / 5D.3 — Mixed workload on a constant VU count. The weighted op mix lives in common.js
// (40/25/15/10/5/5). VUS + DUR come from the environment, so run-load.ps1 drives BOTH the steady
// state and every discrete concurrency level (10/25/50/100/250/500) with the exact same script —
// one k6 process per level, which lines each level's HTTP curve up with its infra samples.
//
// No think-time: VUs loop as fast as the backend answers, so we push to the saturation knee rather
// than a politely-paced user simulation. This is deliberate — the goal is to find where it breaks.
import { userFor, runMixed, SLO } from './common.js';

export const options = Object.assign({}, SLO, {
  vus: Number(__ENV.VUS || 50),
  duration: __ENV.DUR || '30s',
});

export default function () {
  runMixed(userFor(__VU));
}
