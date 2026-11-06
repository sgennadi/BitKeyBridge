"""UniversalDiagnostics portable, opt-in diagnostic reporting."""
from .core import SCHEMA, capture, redact, analyze_local, report_markdown
__version__ = "0.1.0-rc.1"
__all__ = ["SCHEMA", "capture", "redact", "analyze_local", "report_markdown"]
