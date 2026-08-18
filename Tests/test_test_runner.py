import sys
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).parent))

from ea_test import run_tests as run_test_cases
from run_tests import main


class RunTestsStatusTests(unittest.TestCase):
    def test_returns_true_when_every_test_passes(self):
        passing_test = Mock(name="passing_test")
        passing_test.name = "passing"
        passing_test.run_test.return_value = True

        self.assertIs(run_test_cases(Mock(), [passing_test]), True)

    def test_returns_false_when_any_test_fails(self):
        passing_test = Mock(name="passing_test")
        passing_test.name = "passing"
        passing_test.run_test.return_value = True
        failing_test = Mock(name="failing_test")
        failing_test.name = "failing"
        failing_test.run_test.return_value = False

        self.assertIs(run_test_cases(Mock(), [passing_test, failing_test]), False)

    @patch("run_tests.run_tests", return_value=True)
    def test_main_returns_zero_when_every_test_passes(self, run_tests_mock):
        self.assertEqual(main(["run_tests.py", "ColorzCore.exe"]), 0)

    @patch("run_tests.run_tests", return_value=False)
    def test_main_returns_one_when_any_test_fails(self, run_tests_mock):
        self.assertEqual(main(["run_tests.py", "ColorzCore.exe"]), 1)


if __name__ == "__main__":
    unittest.main()
