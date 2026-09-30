# TrayTranslator
**버전**: v1.0 (2026-09-30)

드래그한 텍스트를 핫키로 즉시 번역해 커서 옆 팝업에 보여주는 트레이 상주 프로그램.

- 실행 파일: `TrayTranslator/publish/TrayTranslator.exe` (단일 파일, .NET 10 필요)
  - 앞으로 업데이트는 이 폴더에 덮어씀. 업데이트 시 기존 실행은 트레이 우클릭 → 종료 후 실행
- 메모리: 시작 직후 약 49MB (PyQt안 대비 1/2 이하, 공유 런타임 포함)
- 모델: 설정의 **번역 엔진**에서 선택 (10종)

  | 엔진 | 종류 | 비고 |
  |---|---|---|
  | `Gemini` | LLM | 모델 목록 고정, 무료 티어 |
  | `DeepL` | 번역 전용 | 월 50만 자 |
  | `LM Studio` | LLM (로컬) | 키 불필요, 무제한 |
  | `로컬 (Hy-MT2)` | 내장 | 키 불필요, 무제한 |
  | `DeepSeek` | LLM | 모델 2종(`deepseek-flash`, `deepseek-v4-pro`) |
  | `Groq` | LLM | 서버에서 모델 목록 자동 조회 |
  | `OpenAI` | LLM | 서버에서 모델 목록 자동 조회 |
  | `Google 번역` | 번역 전용 | 모델 선택 없음 |
  | `Papago` | 번역 전용 | Client ID + Secret 2칸 |
  | `Microsoft 번역` | 번역 전용 | 모델 선택 없음 |

  - LLM 엔진은 thinking(추론) 모드를 끄고 요청해 결과에 추론 과정이 섞이지 않게 한다
  - 번역 전용 API는 추론 개념이 없어 항상 즉시 응답한다
  - Papago만 API 키가 두 개(Client ID / Client Secret)라 입력창이 2칸 나온다
  - 내장 엔진 첫 사용 시 llama-server(Vulkan, 약 32MB) 자동 다운로드 + 모델 필요
    (파일 찾기 또는 Q4 1.1GB 다운로드). GPU 전체 오프로드(-ngl 99)로 LM Studio급 속도.
    사용 중 메모리는 약 2GB (별도 프로세스, 앱 종료 시 함께 종료)
  - 내장 엔진 첫 사용 시 llama-server(Vulkan, 약 32MB) 자동 다운로드 + 모델 필요
    (파일 찾기 또는 Q4 1.1GB 다운로드). GPU 전체 오프로드(-ngl 99)로 LM Studio급 속도.
    사용 중 메모리는 약 2GB (별도 프로세스, 앱 종료 시 함께 종료)

## Q4 모델 다운로드 상세 정보
내장 모델(`로컬 (Hy-MT2)`)을 처음 사용하거나 수동으로 다운로드하려면 다음과 같이 두 파일이 자동으로 다운로드됩니다:

1. **llama-server 실행 파일** (약 32MB)
   - URL: `https://github.com/ggml-org/llama.cpp/releases/download/b11254/llama-b11254-bin-win-vulkan-x64.zip`
   - 경로: `%AppData%\TrayTranslator\llama-server\llama-server.exe`
   - 설명: llama.cpp의 Vulkan 최적화 버전으로, GPU를 활용하여 빠른 번역 속도 제공

2. **Hy-MT2-1.8B-Q4_K_M GGUF 모델 파일** (약 1.1GB)
   - URL: `https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF/resolve/main/Hy-MT2-1.8B-Q4_K_M.gguf`
   - 경로: `%AppData%\TrayTranslator\models\hy-mt2.gguf`
   - 설명: 4비트 양자화된 Q4_K_M 버전의 Hy-MT2-1.8B 모델로, 품질과 속도의 균형을 최적화

> 참고: 설정에 이미 모델 파일이 있는 경우 다운로드를 건너뛰고, llama-server는 버전 확인 후 최신 버전이 아니면 다시 다운로드합니다.

## 사용법

1. API 키 발급 (LM Studio는 키 불필요, 서버만 켜두기)
    - Gemini: https://aistudio.google.com (카드 불필요)
    - DeepL: https://www.deepl.com/pro-api (Free 플랜, 카드 등록 필요)
    - LM Studio: 로컬 서버 시작 후 모델 로드 (기본 http://localhost:1234)
    > 모든 API 키는 개인이 직접 발급받아 사용해야 합니다. (LM Studio는 예외)
2. `TrayTranslator.exe` 실행 → 트레이 아이콘 우클릭 → **설정** → **번역 엔진** 선택 →
   키/주소/모델 입력 → **연결 테스트** → 저장
   - 핫키도 설정에서 변경 가능 (Ctrl/Alt/Shift/Win 중 1개 이상 + A-Z, 0-9, F1-F12)
   - 모델 404 오류 시 예비 모델로 자동 전환됨
3. 아무 프로그램에서 텍스트 드래그 → **핫키 (기본 Ctrl+Shift+T)**
   - 선택 텍스트를 접근성(UIA) API로 직접 읽음. 클립보드를 건드리지 않음
   - UIA 미지원 앱에서는 기존 Ctrl+C 방식으로 폴백
   - 커서 옆 팝업에 번역 표시
4. 팝업: 클릭/ESC 닫기, 번역 결과는 자동 클립보드 복사, 3초 후 자동 닫힘
5. 번역 대상 언어: 트레이 우클릭 → **번역 대상 언어** (8개 언어)
6. 긴 파일: 트레이 우클릭 → **파일 번역...** → txt 선택 → 문장 단위로 나눠 순차 번역 후
   `{원본}.{언어코드}.txt` 저장 (예: manual.ko.txt). 조각 크기는 엔진별 최적값
   (DeepL 3만자, Gemini 8천자, LM 2천자, 내장 1.5천자)

> 주의: API 키를 복사한 직후에는 클립보드에 키가 남아있습니다.
> 텍스트를 드래그하지 않고 핫키를 누르면 키가 번역 대상으로 잡히므로,
> 앱이 이를 감지하면 경고를 띄우고 번역하지 않습니다.

## 무료 한도 안내 (2026-09 기준, 계정·시점별로 변동)

- 한도는 **모델별**로 따로 적용됨. 이 앱은 일일 한도가 찬 모델을 만나면 자동으로 다음 모델로 넘어감
- **분당 한도** (보통 분당 10~15회): 연타하면 걸림. 앱이 구글 안내 대기시간 후 **자동 재시도**함
- **일일 한도**: 최근 삭감되어 모델·계정에 따라 수십 회 수준일 수 있음. 태평양 자정(한국 오후 4~5시) 리셋
- 같은 문장 반복 번역은 API 호출 없이 캐시로 처리. 설정 화면에 오늘 호출 횟수 표시

## 문제 해결

- "복사하지 못했습니다"가 뜨면: 선택 영역 복사가 안 된 것입니다. 텍스트를 확실히
  드래그한 뒤 핫키를 누르세요. 관리자 권한으로 실행한 프로그램(관리자 메모장 등)은
  일반 권한 앱에서 Ctrl+C를 보낼 수 없으므로, 그 경우 이 앱도 관리자 권한으로 실행하세요.
- "클립보드에 API 키가"가 뜨면: 복사해둔 키가 번역 대상으로 잡힌 것입니다.
  번역할 텍스트를 드래그한 뒤 핫키를 누르세요.
- 설정의 **연결 테스트**로 키·모델 유효성을 먼저 확인하세요.
- 진단 로그: `%AppData%\TrayTranslator\app.log` — 번역이 이상하면 이 파일 끝부분을 확인하세요.

## 데이터 위치

- exe 옆 `data\` 쓰기 가능 → portable 모드 (설정·로그·모델·서버 전부 `data\` 아래)
- `data\settings.json`이 이미 있으면 항상 portable, 기존 `%AppData%\TrayTranslator` 설정이 있으면 그대로 유지
- exe 옆 쓰기 불가(Program Files 등) → `%AppData%\TrayTranslator` 사용

## 자동 시작 등록

`Win+R` → `shell:startup` → `TrayTranslator.exe` 바로가기 복사.

## 번역 엔진 추가 방법

엔진 식별은 문자열이 아니라 `AppSettings.ProviderKind` enum으로 처리합니다.
모든 분기에 `default: throw`가 있어, 새 엔진을 추가하고 분기를 빠뜨리면
**화면이 이상해지는 대신 즉시 예외로 드러납니다.**

추가할 때 수정할 곳(`dotnet build`가 통과할 때까지):

1. `AppSettings.cs`
   - `ProviderKind` enum에 값 추가
   - `DisplayName()`에 표시 이름 추가 (없으면 throw)
   - `Providers` 배열에 표시 문자열 추가
   - `NeedsKey` / `HasModel` / `IsOpenAiCompat` / `IsTranslateApi` 판정에 포함 여부 결정
   - OpenAI 호환이면 `OpenAiServices` 테이블에 항목 추가 (기본 주소·모델 목록)
2. `AppSettings.cs` — 키가 필요하면 `XxxKey` + `XxxKeyEnc` 프로퍼티 추가 (DPAPI 암호화)
3. `TrayAppContext.cs` — 3곳 switch에 케이스 추가
   - 조각 크기 / `TranslateAsync()` 번역 분기 / `ActiveKey` 키 조회
4. `SettingsForm.cs`
   - `LoadFieldsFromSettings()`, `SaveFieldsToSettings()` — 설정 ↔ 메모리
   - `OnTest()` — 연결 테스트
   - UI 표시 여부는 `AppSettings`의 판정 함수와 `LayoutForProvider()`가 처리
5. 클라이언트 작성 — LLM이면 `OpenAiCompatClient`, 번역 전용이면 전용 클라이언트

> `SettingsForm`은 엔진별 설정을 `EngineFields` 객체로 모아둔다.
> 새 엔진을 추가할 때 화면 분기가 늘 필요 없고, 판정 함수만 맞추면
> 입력창·모델 드롭다운·서버 주소가 자동으로 나타난다/사라진다.

> 주의: 엔진 전환 시 `StashPreviousProviderFields()`가 **직전 엔진**을 기준으로
> 입력창 값을 보관한다. ComboBox는 선택 즉시 `Provider`가 새 값으로 바뀌므로, 현재 엔진이 아니라
> `PrevEngine`을 기준으로 해야 값이 섞이지 않는다.

## 빌드

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```