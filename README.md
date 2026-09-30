# TrayTranslator
**버전**: v1.6 (2026-09-30)

드래그한 텍스트를 핫키로 즉시 번역해 커서 옆 팝업에 보여주는 트레이 상주 프로그램.

- 실행 파일: `TrayTranslator/publish/TrayTranslator.exe` (단일 파일, .NET 10 필요)
  - 앞으로 업데이트는 이 폴더에 덮어씀. 업데이트 시 기존 실행은 트레이 우클릭 → 종료 후 실행
- 메모리: 트레이 상주 시 Private 약 14MB (Working set 약 53MB, .NET 공용 런타임 포함).
  내장 엔진 사용 중에는 별도 프로세스(llama-server)가 약 2.3GB 추가 사용
  (Q8 모델 1.8GB + KV 256MB + 연산 버퍼, ctx 4096 기준. 앱 종료 시 함께 종료)
- 모델: 설정의 **번역 엔진**에서 선택 (10종)

  | 엔진 | 종류 | 비고 |
  |---|---|---|
  | **AI 모델** | | |
  | `DeepSeek` | LLM | 모델 2종(DeepSeek Flash / V4 Pro), thinking off |
  | `Gemini` | LLM | 모델 3종(Flash-Lite 2.5 / 2.0 / Flash 2.5), 무료 티어 |
  | `Groq` | LLM | 서버에서 모델 목록 자동 조회 |
  | `LM Studio` | LLM (로컬) | 키 불필요, 무제한 |
  | `OpenAI` | LLM | 서버에서 모델 목록 자동 조회 |
  | **번역 전용** | | |
  | `DeepL` | 번역 전용 | 월 50만 자 |
  | `Google 번역` | 번역 전용 | 모델 선택 없음 |
  | `Microsoft 번역` | 번역 전용 | 지역 리소스는 리전 입력 |
  | `Papago` | 번역 전용 | Client ID + Secret 2칸, 원본 언어 직접 선택 |
  | `오프라인 번역 (무료)` | 내장 | 키 불필요, 무제한 |

  - LLM 엔진은 thinking(추론) 모드를 끄고 요청해 결과에 추론 과정이 섞이지 않게 한다
  - 번역 전용 API는 추론 개념이 없어 항상 즉시 응답한다
  - Papago만 API 키가 두 개(Client ID / Client Secret)라 입력창이 2칸 나온다
  - Papago는 원본 언어를 직접 골라야 한다(자동 감지 없음). 조합 제한(ko<->en, ko<->zh-CN, ko<->es, ko<->fr, ko<->vi, en<->ja, en<->fr 등)과 1회 5,000자 제한을 넘기면 N2MT06/N2MT08 에러가 그대로 표시된다
  - Microsoft 번역은 지역 리소스라면 설정에 리전(예: koreacentral)을 입력해야 한다. 비워두면 전역 리소스로 시도한다
  - 내장 엔진 첫 사용 시 llama-server(Vulkan, 약 32MB) 자동 다운로드 + 모델 필요
    (파일 찾기 또는 Q4 1.1GB 다운로드). GPU 전체 오프로드(-ngl 99)로 LM Studio급 속도.
    사용 중 메모리는 약 2GB (별도 프로세스, 앱 종료 시 함께 종료)

## Q4 모델 다운로드 상세 정보
내장 모델(`오프라인 번역 (무료)`, Hy-MT2)을 처음 사용하거나 수동으로 다운로드하려면 다음과 같이 두 파일이 자동으로 다운로드됩니다:

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

1. API 키 발급 (LM Studio·오프라인 번역은 키 불필요)
    - Gemini: https://aistudio.google.com (카드 불필요)
    - DeepL: https://www.deepl.com/pro-api (Free 플랜, 카드 등록 필요)
    - DeepSeek: https://platform.deepseek.com
    - Groq: https://console.groq.com/keys
    - OpenAI: https://platform.openai.com/api-keys
    - Google 번역: https://console.cloud.google.com (Translation API 사용 설정 후 키 발급)
    - Papago: https://developers.naver.com (애플리케이션 등록 → Papago NMT, Client ID + Secret)
    - Microsoft 번역: https://azure.microsoft.com (Translator 리소스 → 키, 지역 리소스는 리전도 입력)
    - LM Studio: 로컬 서버 시작 후 모델 로드 (기본 http://localhost:1234)
    - 오프라인 번역: 설정에서 모델 파일 찾기 또는 Q4 다운로드 (키 불필요)
    > 모든 API 키는 개인이 직접 발급받아 사용해야 합니다. (LM Studio·오프라인 번역은 예외)
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
   (DeepL 3만자, Gemini 8천자, LM 2천자, 오프라인 ctx 비례, Papago 4천자)

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

## 모델별 API 추가 가이드

### LLM 엔진 (OpenAI 호환: DeepSeek, Groq, OpenAI, LM Studio)

- `AppSettings.cs` ProviderKind enum에 값을 추가하고 `DisplayName()`, `Providers` 배열에 문자열을 추가합니다.
- `NeedsKey` / `HasModel` / `IsOpenAiCompat` / `IsTranslateApi` 판정 함수에 해당 엔진을 포함하거나 제외합니다.
- OpenAI 호환 엔진이면 `OpenAiServices` 테이블에 기본 서버 주소와 고정 모델 목록을 추가합니다.
- `TrayAppContext.cs` Provider switch에 케이스를 추가해 `TranslateAsync()` 분기 처리하고 `ActiveKey` 조회 로직을 업데이트합니다.
- 클라이언트(`Clients/`)는 `OpenAiCompatClient`을 상속받아 `TranslateAsync()`를 구현합니다(LLM인 경우) 또는 전용 클라이언트를 사용합니다.

### 번역 전용 API (DeepL, Google 번역, Papago, Microsoft 번역)

- `AppSettings.cs`에 ProviderKind 값을 추가하고 `DisplayName()`, `Providers`에 문자열을 추가합니다.
- `NeedsKey` / `HasModel` / `IsOpenAiCompat` / `IsTranslateApi` 함수에 포함 여부를 결정합니다.
- 키가 필요하면 `XxxKey` / `XxxKeyEnc` 프로퍼티를 추가(DPAPI 암호화).
- `TrayAppContext.cs` Provider switch에 케이스를 추가하고 조각 크기(`ChunkSize`) 분기 로직을 업데이트합니다.
- 각 전용 클라이언트(`DeepLClient`, `GoogleTranslateClient`, `PapagoClient`, `MsTranslatorClient`)는 이미 `TranslateAsync(string, string)`을 구현하고 있습니다. Papago는 Client ID + Client Secret 2칸을 입력받습니다.

### 내장 엔진 (오프라인 번역)

- `AppSettings.cs`에 `BuiltinModelPath` 및 `BuiltinContextSize` 유지 (이미 기본 제공).
- llama-server 다운로드 및 버전 관리는 `BuiltinClient.DownloadServerAsync()` / `DownloadModelAsync()`을 이용합니다.
- 설정에서 Q4 모델 파일(Q4 1.1GB)이 자동 다운로드되며, llama-server(Vulkan 약 32MB)도 최초 1회 다운로드됩니다.
- 엔진 전환 시 `StashPreviousProviderFields()`가 직전 엔진을 기준으로 입력값을 보관하므로, ComboBox 선택 시 `PrevEngine`을 기준으로 값이 섞이지 않도록 주의합니다.

> **주의**: 새 엔진을 추가하고 `default: throw` 분기를 빠뜨리면 화면이 아닌 즉시 예외가 터지니, `dotnet build`가 통과할 때까지 모든 분기에서 케이스를 처리했는지 확인한다.

## 빌드

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```