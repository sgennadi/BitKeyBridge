import unittest
from unittest.mock import patch
from datetime import datetime, timezone
from collections import Counter
from universal_diagnostics import issue_review

PROJECT = 'sgennadi/GPOSettingsExplorer'
NOW = datetime.now(timezone.utc).isoformat()
FP = '0'*64


class RuntimeReviewTests(unittest.TestCase):
    def test_groups_events_without_copying_body(self):
        issues = [
            {"number": 3, "created_at": NOW, "body": "<!-- udx-report-id:x -->\nDiagnostic-Fingerprint: `"+FP+"`\nsecret PASSWORD 123"},
            {"number": 4, "created_at": NOW, "body": "## GPO Settings Explorer - opt-in diagnostics\nDiagnostic-Fingerprint: "+FP},
            {"number": 5, "created_at": NOW, "body": "unrelated\nDiagnostic-Fingerprint: "+FP},
        ]
        with patch('universal_diagnostics.issue_review.request_json', return_value=issues):
            counts, samples = issue_review.collect(PROJECT, 'fake')
        self.assertEqual(2, counts[FP])
        text = issue_review.summarize(counts, samples, 30)
        self.assertIn('#3', text)
        self.assertIn('#4', text)
        self.assertNotIn('PASSWORD', text)
        self.assertNotIn('secret', text)

    def test_no_repeat_no_issue(self):
        text = issue_review.summarize(Counter({FP: 1}), {FP: [3]}, 30)
        self.assertIn('No fingerprint repeated', text)

    def test_update_existing(self):
        issues = [{"number": 8, "title": issue_review.TITLE,
                   "body": issue_review.MARKER+' older',
                   "html_url": "https://github.com/sgennadi/GPOSettingsExplorer/issues/8"}]
        with patch('universal_diagnostics.issue_review.request_json', side_effect=[issues, {}]) as req:
            self.assertEqual('https://github.com/sgennadi/GPOSettingsExplorer/issues/8',
                             issue_review.upsert(PROJECT, 'fake', issue_review.MARKER+' new'))
            self.assertEqual('PATCH', req.call_args.args[0])

    def test_scan_overflow_fails_closed(self):
        with patch('universal_diagnostics.issue_review.request_json', return_value=[{}]*100):
            with self.assertRaises(Exception):
                issue_review.collect(PROJECT, 'fake')

if __name__ == '__main__':
    unittest.main()
