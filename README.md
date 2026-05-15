# Embedded SW Tester

임베디드 SW 테스트케이스를 YAML로 작성하고, 이를 실행 가능한 resolved package로 컴파일하고 mock runtime으로 실행한 뒤 로컬 리포트를 생성하기 위한 프로토타입입니다.

현재 저장소의 구현 범위는 **Phase 3: Python DSL Compiler + Runtime Core + Report Pipeline**입니다. 전체 제품 설계는 C#/.NET Windows IDE, Python 실행 엔진, Trace32/CANoe/INCA/Serial 어댑터를 목표로 하지만, 이 커밋의 실행 가능한 코드는 YAML DSL 컴파일러, 순수 Python runtime, 리포트 생성, CLI에 집중되어 있습니다.

## 현재 지원 범위

- YAML 테스트 파일 로드
- 상대 경로 기반 function library import
- 축약 명령 문법 정규화
- function symbol table 생성
- unknown command 진단
- function call 반환 매핑 검증
- resolved package JSON 출력
- `set`, `call`, `assert.eq`, `assert.gt`, `assert.fail`, `log.*`, `delay` 실행
- testcase/function frame과 local variable scope
- command event와 testcase result JSON 출력
- adapter-category 명령의 mock event 처리
- `run.json`, `resolved-package.yaml`, testcase result JSON, `summary.html` 리포트 생성
- pytest 기반 회귀 테스트

## 프로젝트 구조

```text
docs/
  design/
    embedded-sw-tester-detailed-design.md
  superpowers/
    plans/
      2026-05-15-embedded-sw-tester-phase1.md
samples/
  boot-smoke.yaml
  libs/
    common-power-sequence.yaml
src/
  embsw_tester/
    cli.py
    dsl/
      catalog.py
      compiler.py
      models.py
    reports/
      models.py
      writer.py
    runtime/
      expressions.py
      models.py
      runner.py
tests/
  test_cli.py
  test_compiler.py
  test_reports.py
  test_runtime.py
```

## 개발 환경 준비

Python 3.9 이상이 필요합니다.

macOS/Linux:

```bash
python3 -m venv .venv
.venv/bin/python -m pip install --upgrade pip
.venv/bin/python -m pip install -e ".[dev]"
```

Windows PowerShell:

```powershell
py -3.9 -m venv .venv
.\.venv\Scripts\python -m pip install --upgrade pip
.\.venv\Scripts\python -m pip install -e ".[dev]"
```

## 테스트 실행

```bash
.venv/bin/python -m pytest
```

Windows PowerShell:

```powershell
.\.venv\Scripts\python -m pytest
```

## 샘플 YAML 컴파일

설치 후 console script를 사용합니다.

```bash
.venv/bin/embsw-tester compile samples/boot-smoke.yaml --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester compile samples\boot-smoke.yaml --json
```

editable install 없이 바로 실행하려면 `PYTHONPATH`를 지정할 수 있습니다.

```bash
PYTHONPATH=src .venv/bin/python -m embsw_tester.cli compile samples/boot-smoke.yaml --json
```

정상 컴파일되면 `diagnostics`가 빈 배열이고, import된 `power_on` function과 `boot_smoke` testcase가 포함된 resolved package JSON이 출력됩니다.

## 샘플 YAML 실행

mock runtime으로 샘플 테스트케이스를 실행합니다.

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester run samples\boot-smoke.yaml --json
```

정상 실행되면 `status`가 `passed`이고, `testcase_results`에 실행된 command event와 최종 local variable snapshot이 포함됩니다. 현재 외부 툴 adapter 명령은 실제 장비를 제어하지 않고 mock event로 기록됩니다.

## 리포트 생성

`run` 명령에 `--reports-root`를 지정하면 실행 결과를 파일로 저장합니다.

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id boot-smoke-local --reports-root reports --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester run samples\boot-smoke.yaml --run-id boot-smoke-local --reports-root reports --json
```

출력 위치:

```text
reports/boot-smoke-local/
  summary.html
  run.json
  resolved-package.yaml
  testcase-results/
    boot_smoke.json
  attachments/
  raw-logs/
```

`run.json`은 기계용 실행 원본이고, `summary.html`은 사람이 빠르게 확인하기 위한 요약입니다. `attachments/`와 `raw-logs/`는 이후 실제 외부 툴 adapter가 생성할 증적을 저장하기 위해 미리 만들어집니다.

## YAML 예시

```yaml
imports:
  - libs/common-power-sequence.yaml

testcases:
  - name: boot_smoke
    steps:
      - call:
          function: power_on
          args:
            channel: 1
          out:
            voltage_ok: power_ready
      - assert.eq:
          left: "${power_ready}"
          right: true
```

명령은 `cmd:` 키를 쓰지 않고 명령 이름 자체를 YAML key로 사용합니다. 컴파일러는 이를 내부적으로 `type`과 `args`를 가진 명령 객체로 정규화합니다.

## 설계 문서

- 상세 설계서: `docs/design/embedded-sw-tester-detailed-design.md`
- Phase 1 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase1.md`
- Phase 2 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase2-runtime.md`
- Phase 3 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase3-report-pipeline.md`

## 다음 구현 단계

다음 단계는 Adapter Framework입니다.

- 공통 adapter interface 정의
- adapter-category 명령 dispatch 구조화
- Serial mock/real adapter 분리
- raw evidence 저장 연동
