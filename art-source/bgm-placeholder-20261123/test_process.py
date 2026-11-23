import unittest

import numpy as np

from process import make_loop, metrics


class LoopTests(unittest.TestCase):
    def test_rotated_crossfade_has_exact_duration_and_natural_boundary(self):
        rate = 1000
        time = np.arange(47600) / rate
        stereo = np.column_stack((np.sin(2 * np.pi * 11.3 * time), np.sin(2 * np.pi * 7.1 * time))) * 0.1
        loop, edit = make_loop(stereo, rate, 32, 1.5)
        self.assertEqual(len(loop), 32000)
        start = edit["sourceStartFrame"]
        original_step = stereo[start + 1500] - stereo[start + 1499]
        np.testing.assert_allclose(loop[0] - loop[-1], original_step, atol=1e-12)

    def test_rejects_short_input(self):
        with self.assertRaises(AssertionError):
            make_loop(np.zeros((5000, 2)), 1000, 32, 1.5)

    def test_rejects_silence(self):
        with self.assertRaises(AssertionError):
            metrics(np.zeros((5000, 2)), 1000)

    def test_rejects_phase_cancellation_and_nonfinite_input(self):
        wave = np.sin(np.arange(5000)) * 0.1
        with self.assertRaises(AssertionError):
            metrics(np.column_stack((wave, -wave)), 1000)
        with self.assertRaises(AssertionError):
            metrics(np.full((5000, 2), np.nan), 1000)


if __name__ == "__main__":
    unittest.main()
