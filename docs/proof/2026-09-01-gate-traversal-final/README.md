# Headed continuous gate traversal proof

The development Windows player performed ordinary `NavMeshAgent.Move` pulses over the single-scene ground. No gate NavMeshLink or hero warp was present. Every pulse produced a screenshot.

| Entrance | Moves | Exit time (s) | Start to final image delta | Consecutive deltas |
|---|---:|---:|---:|---|
| north | 22 | 2.894 | 43.334 | 7.489, 2.388, 4.858, 7.471, 10.519, 8.269, 5.504, 22.301, 20.098, 12.29, 17.668, 19.395, 19.648, 23.554, 25.019, 25.535, 21.196, 18.509, 16.501, 8.613, 8.14, 8.683 |
| south | 22 | 2.902 | 25.898 | 18.819, 19.555, 19.357, 19.996, 23.558, 28.149, 25.573, 21.803, 22.156, 18.823, 13.936, 13.289, 14.687, 17.584, 19.929, 21.781, 22.791, 19.807, 13.49, 8.074, 8.513, 8.366 |
| east | 22 | 2.841 | 38.587 | 7.941, 22.165, 27.694, 17.949, 13.077, 10.282, 7.537, 6.129, 8.923, 8.912, 10.583, 13.502, 14.275, 16.084, 18.251, 20.365, 20.374, 20.094, 19.22, 9.469, 9.722, 9.343 |
| west | 22 | 2.857 | 25.582 | 15.327, 17.081, 20.685, 24.416, 27.327, 32.269, 29.622, 24.681, 26.093, 24.057, 22.473, 21.93, 21.904, 19.443, 20.38, 21.374, 22.934, 17.699, 14.327, 8.104, 8.12, 8.422 |

Raw positions/timings: [gate-traversal.csv](gate-traversal.csv)
