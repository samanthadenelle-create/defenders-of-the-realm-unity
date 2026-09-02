# Headed continuous gate traversal proof

The development Windows player performed ordinary `NavMeshAgent.Move` pulses over the single-scene ground. No gate NavMeshLink or hero warp was present. Every pulse produced a screenshot.

| Entrance | Moves | Exit time (s) | Start to final image delta | Consecutive deltas |
|---|---:|---:|---:|---|
| north | 22 | 2.896 | 10.088 | 1.798, 0.452, 1.234, 1.188, 2.195, 1.997, 1.256, 5.143, 4.586, 2.845, 4.255, 4.544, 4.603, 5.597, 6.043, 6.133, 5.127, 4.568, 3.877, 2.026, 2.021, 2.126 |
| south | 22 | 2.856 | 6.912 | 4.67, 5.123, 5.367, 5.018, 5.4, 6.7, 6.674, 5.818, 5.985, 5.788, 3.762, 3.622, 3.86, 4.781, 5.68, 6.455, 7.222, 5.725, 3.968, 2.35, 2.395, 2.336 |
| east | 22 | 2.904 | 8.184 | 1.742, 5.195, 6.002, 3.691, 2.369, 1.823, 1.296, 1.333, 1.913, 1.837, 2.203, 3.094, 3.16, 3.665, 4.155, 4.573, 4.854, 4.77, 4.56, 2.099, 2.071, 2.119 |
| west | 22 | 2.946 | 5.266 | 3.15, 3.761, 4.721, 5.537, 6.575, 6.954, 6.085, 5.54, 5.808, 4.675, 4.345, 4.653, 4.531, 3.929, 4.459, 4.776, 5.206, 4.209, 3.37, 1.77, 1.723, 1.838 |

Raw positions/timings: [gate-traversal.csv](gate-traversal.csv)
