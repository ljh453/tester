from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Dict


@dataclass(frozen=True)
class ReportArtifacts:
    report_dir: Path
    run_json: Path
    resolved_package_yaml: Path
    summary_html: Path

    def to_dict(self) -> Dict[str, str]:
        return {
            "report_dir": str(self.report_dir),
            "run_json": str(self.run_json),
            "resolved_package_yaml": str(self.resolved_package_yaml),
            "summary_html": str(self.summary_html),
        }
