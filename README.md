# sogang-workshop-25-2-team2_2nd
어디갔어, 내 고양이?

레벨마다 **사진 속 고양이**를 집 안에서 찾아내는 1인칭 고양이 찾기 게임입니다. (Unity 2022.3.62f3)

## 화면 구성

| 씬 | 화면 |
| --- | --- |
| IntroScene | 타이틀 → 스토리(처음 한 번) → **레벨 선택** / 고양이 앨범 / 게임 방법 / 설정 |
| MainScene | **미션 안내**(찾을 고양이 사진) → 플레이(HUD) → 일시정지·설정 → **결과**(별, 기록) 또는 **시간 초과** |
| OutroScene | 마지막 레벨을 깨면 엔딩 |

추가 화면들은 코드(`MenuUI`, `GameHUD`, `SettingsPanel`)가 실행 중에 만들기 때문에 씬 파일을 따로 고칠 필요가 없습니다.

## 레벨

| 레벨 | 찾을 고양이 | 특징 |
| --- | --- | --- |
| 1. 토리를 찾아라 | 토리 | 튜토리얼, 시간 무제한, 힌트 3번 |
| 2. 턱시의 숨바꼭질 | 턱시 | 3분, 시작 위치에서 안 보이는 곳에 숨음 |
| 3. 미미와 여름 | 미미, 여름 | 4분, 두 마리 |
| 4. 옆집 고양이 주의! | 토리 | 3분, 색이 다른 옆집 고양이 2마리 (잘못 누르면 -15초) |
| 5. 모두 모여라! | 네 마리 모두 | 5분, 힌트 1번, 옆집 고양이 2마리 → 엔딩 |

- 레벨 2부터는 판마다 고양이가 다른 곳에 숨습니다 (`CatSpawner`가 걸어갈 수 있는 바닥과 가구 위를 자동으로 찾음).
- **H** 키 힌트: 가장 가까운 고양이가 "야옹~" 울고(3D 사운드) 방향 화살표가 4초간 보입니다.
- 별: 목표 시간 안에 클리어 +1, 힌트·오답 없이 클리어 +1 (최소 1개). 기록은 PlayerPrefs에 저장됩니다.
- 집 안의 단서 5개를 찾으면 점수와 함께 **고양이 앨범**에 기록됩니다.

### 레벨/고양이 수정하기

`Assets/Resources/LevelDatabase.asset`을 선택하면 Inspector에서 바로 고칠 수 있습니다.

- 레벨 제목, 설명, **고양이 사진**, 고양이 이름과 색(tint), 마리 수
- 제한 시간, 힌트 수, 옆집 고양이 수, 숨김 비율, 별 3개 목표 시간
- `Fixed Spawn Positions`에 좌표를 넣으면 자동 탐색 대신 그 위치에 고양이를 둡니다.

새 고양이 사진을 넣으려면 이미지를 `Assets/` 아래에 추가한 뒤 레벨의 `Photo` 칸에 끌어다 놓으면 됩니다. 레벨을 추가하면 레벨 선택 화면에 자동으로 카드가 늘어납니다.

## 조작

기본은 캐릭터가 보이는 어깨 너머 3인칭 시점이고, **V**로 1인칭(손이 보이는 시점)과 전환할 수 있습니다.

타이틀·스토리 화면: 클릭 또는 ESC·백스페이스·스페이스로 넘기기 · 열린 메뉴는 ESC로 닫기

WASD·↑↓ 걷기 · 마우스·←→ 둘러보기 · E 상호작용(고양이 안아주기·단서 살펴보기·문/서랍 여닫기, 화면 아래 [E] 안내가 뜰 때) · Space 점프 · Ctrl 앉기(살금살금 다가가면 고양이가 덜 눈치챕니다) · H 힌트 · V 시점 전환 · ESC/P 일시정지

## 웹 배포 (GitHub Pages)

`.github/workflows/deploy-webgl.yml`이 `main`에 push될 때마다 WebGL로 빌드해서 GitHub Pages에 올립니다.
배포 주소: **https://lemondkel.github.io/sogang-workshop-25-2-team2_2nd/**

처음 한 번만 설정이 필요합니다.

1. **Unity 라이선스 시크릿 등록** — 저장소 Settings → Secrets and variables → Actions 에 아래 3개를 추가합니다.
   - `UNITY_EMAIL`, `UNITY_PASSWORD`: Unity 계정 이메일/비밀번호
   - `UNITY_LICENSE`: Unity 라이선스 파일(`.ulf`) 내용 전체. 발급 방법은 [GameCI 문서](https://game.ci/docs/github/activation) 참고 (Personal 라이선스는 Unity Hub로 활성화한 뒤 생기는 `Unity_lic.ulf` 파일 내용을 붙여넣으면 됩니다).
2. **Pages 켜기** — Settings → Pages → Build and deployment → Source 를 **GitHub Actions** 로 바꿉니다.
3. Actions 탭에서 **Build & Deploy WebGL** 워크플로를 실행(또는 `main`에 push)합니다. 첫 빌드는 30분~1시간 정도 걸립니다.

시크릿이 없으면 워크플로는 실패하지 않고 빌드만 건너뜁니다.
