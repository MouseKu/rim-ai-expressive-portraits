## 1. RIM AI Expressive Portraits

---

<img src="Resources/About/Preview.png" title="" alt="Rim AI Expressive Portraits preview" data-align="center">

- **RIM AI Expressive Portraits**는 정착민의 **게임 내 모습(스프라이트), 건강 상태, 나이, 성별 등의 정보**를 바탕으로 AI 초상화를 생성해주는 모드입니다.

![](.img/generation.webp)

- 정착민의 **게임 내 모습, 건강 상태, 나이, 성별 등의 정보**를 조합해 Gemini, OpenAI 또는 ComfyUI로 초상화를 생성합니다.

![](.img/shfit.webp)

- 또한 정착민의 상태에 따라 **다른 모습을 표시 가능합니다.**

## 2. 요구 사항

---

- RimWorld 1.5 or 1.6

- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

- 이미지 생성이 가능한 Provider:
  
  - OpenAI API key
  - Google AI API key
  - local ComfyUI server

## 3. 기능

---

### 3-1. 기본 초상화 생성

![](.img/main-v2.jpg)

- 정착민의 행동 목록에 AI 초상화 메뉴를 여는 버튼이 있습니다.

- 정착민을 처음 열면 현재 Base Provider의 기본 프롬프트를 옵션에서 가져옵니다. 정착민과 Provider마다 Base 프롬프트를 따로 수정하거나, Extra에 추가 정보를 넣을 수 있습니다. 정착민은 개별 설정값을 저장하고 있으며, 첫 초상화 생성시 개별 설정값이 저장됩니다.

- 생성 방법은 다음과 같습니다.
  
  - Generate new Base 버튼을 누릅니다.
  
  - 기다리면 자동 적용됩니다.
  
  - 갤러리에서 더블클릭이나 Confirm을 통해서 바꿀 수 있습니다.

- 그 외 지원 기능은 다음과 같습니다.
  
  - 선택한 이미지 재생성
  - 배경색 삭제
  - 이미지 삭제
  - 이미지 폴더 열기
  - 컨트롤 클릭, 쉬프트 클릭을 통한 여러 이미지 선택.

### 3-2. 감정 초상화 생성

생성된 감정으로 부터 여러 파생된 감정 상태 초상화를 생성 가능합니다.

![](.img/expression-multi.png)

- 여러 감정 표현이 있지만, 옵션에서 체크해야 보입니다. 기본으로 Low(무드 낮음)과 High(무드 높음)만 켜져있습니다.

- 생성 방법은 다음과 같습니다.
  
  - Base 이미지를 선택합니다.
  
  - Generation emotions from Base를 누릅니다.
  
  - 생성할 감정만 체크하고 생성합니다.
  
  - 생성이 완료된 감정은 자동 적용됩니다. 화살표키로 현재 선택 감정을 넘기면서 더블클릭 혹은 Confirm으로 현재 보는 감정을 적용 가능합니다.

![](.img/make-expression2.png)

### 3-3. 배경 제거

![](.img/bg-remove-v2.jpg)

- OpenAI의 경우 배경을 자동으로 투명으로 지정해서 보낼 수 있지만, 어떤 Provider는 그게 안되기도 합니다. 
  
  - 미리 보기를 좌클릭하면 자동으로 유사 색상을 제거합니다.
  
  - Backgroudn sensitivity: 높을수록 더 공격적으로 색상을 날립니다.
  
  - Edge erosion: 배경색과 접한 부분을 추가로 날립니다. 
  
  - Undo: 마우스 클릭을 한 번 되돌립니다.
  
  - Auto remove enclosed areas: 자동으로 캐릭터 내부에 둘러쌓인 영역도 색상이 비슷하면 제거합니다.
  
  - 배경 제거 후 Align 버튼 누를 시 아래 면을 기준으로 이미지를 재정렬할 수 있습니다.

![remove-bg-v2.webp](C:\Users\Mint\Documents\GitHub\rim-ai-portrait\.img\remove-bg-v2.webp)

### 3-4. OpenAI

![](.img/open-ai-examples1.png)

- openai의 sunburst의 low quality가 가장 많이 테스트한 모델입니다. 투명 배경을 지원하며 테스트 된 모델 중에 low quality를 이용할 시 가격 대비 품질이 가장 우수합니다. medium/high는 가격이 꽤 비쌉니다.

### 3-5. Google Gemini

![](.img/gemini-examples.png)

- 투명 배경을 지원하지 않습니다. 프롬프트만으로 투명 배경을 뽑기가 매우 어렵기 때문에 OpenAI과 경쟁하다가 테스트 단계에서 기본 옵션에서 밀려났습니다.

- 단가가 openai 보다 약 2배 정도 비싸지만 일관성은 잘 지킵니다.

### 3-6. Comfy(Local Model)

![](.img/comfy-example.png)

- 로컬 모델 셋업은 정말 많은 시간과 노력을 투자해야합니다. 제작자도 이걸로 아직 원하는 퀄리티를 뽑아내지 못했습니다.

- 테스트에 사용한 파이프라인을 공개할 예정입니다.

### 3-7. NovelAI(NAI)

- NAI는 구독제입니다. v4.5 기준으로 프롬프트를 튜닝하였습니다.

- NAI 경우 OpenAI/Google과 달리 "그림 생성 모델"로서 특징이 더 강해서 유연성이 떨어집니다.

- 이 유연성을 극복하기 위해서 Inpaint와 Tag Extractor 기능을 제공합니다.

- Inpaint: 바꿀 영역을 드래그로 선택합니다. Inpaint Strength에 따라 더 강하게 바꿉니다. Sleep 등 눈을 완전 바꾸는 표현은 Strength를 높게 주어야합니다. Inpaint의 Strength가 강해질수록 일관성 유지 능력이 떨어져서 Extra Tag를 입력할 수 있게 하고 있습니다. 

![inpaint.jpg](.img\inpaint.jpg)

- TagExtractor: Google/OpenAI/LocalLLM 등으로 입력된 그림을 인식하고 positive/negative 태그로 변환하는 기능입니다. 기본적으로 꺼져있습니다.

- 랫킨처럼 생김새 특성이 강하면 NAI는 잘 인식하지만, 바닐라 캐릭터처럼 너무 단순하게 생기면 캐릭터 인식을 잘 못하는 경향이 있습니다. 자연어로 된 프롬프트와 이미지를 태그로 바꿔서 감정/Base 생성 시 자동으로 덧붙여서 요청을 보내는 기술입니다.

- Google gemini flash lite 3.5 기준으로 실행이 검증되었습니다.

![tag-extractor.jpg](C:\Users\Mint\Documents\GitHub\rim-ai-portrait\.img\tag-extractor.jpg)

## 4. 옵션

---

### 4-1. 기본 옵션

![](.img/base-settings1.png)

- Provider는 OpenAI/Gemini/Comfy(Local Model)/NAI 중에 고를 수 있습니다.

- 기본으로 OpenAI와 gpt-image-2.5-sunburst 으로 설정되어있으며, API Key를 넣으면 됩니다.

![](.img/base-settings2.png)

- 어떤 정보가 프롬프트에 자동으로 반영될지 고를 수 있습니다. 기본적으로 캐릭터의 인게임 이미지와 텍스트 정보(성별, 나이) 등을 동시에 반영해서 이미지를 생성합니다.

### 4-2. 감정 옵션

![](.img/expression-settings1.png)

- 정착민의 상태에 따라 초상화를 교체하게 할 수 있습니다. 총 9가지가 있는데, 다켜면 꽤 돈이 많이 나갈거에요. 이 기능은 끄고 사용해도 무방합니다.

![](.img/expression-settings2.png)

- 감정 표현 생성도 마찬가지로 Provider를 따로 공급 가능합니다. 둘 다 같은 Provider를 쓸 수도 있고, 서로 섞어 쓸 수도 있습니다.

### 4-3. Style Reference

- 폴더를 지정하면 해당 폴더에 있는 그림의 "그림체"를 참조하는 기능입니다.
  
  - 1~2장이면 충분합니다. 많아도 4장 이상 넣지 마세요. NAI는 한장만 적용 가능합니다. 디폴트는 1~2장이 들어갑니다.
  
  - 파일 해상도를 작게 하세요. 디폴트 스타일은 256x256을 쓰고 있습니다.
  
  - `Using Default Style`일 때 OpenAI는 스타일 참조 이미지를 보내지 않고, Google은 모드에 포함된 Style 폴더를 사용합니다. OpenAI에 스타일을 적용하려면 사용자 지정 폴더를 입력하세요.

### 4-4. 그 외 옵션

![](.img/position-settings.png)

- 초상화 크기와 위치를 조절 가능합니다.

## 4. 설치 방법

---

- 준비 중

## 5. Comfy 설정 팁

---

- **로컬 모델은 설정부터 꽤 많은 시간과 노력이 필요합니다.** 여기에 감정별 초상화의 일관성까지 유지하려면 난이도가 더욱 높아지기 때문에, 관련 분야에 익숙하지 않다면 로컬 모델 사용은 권장하지 않습니다. 

- 그래도 참고할 수는 있도록, 제가 소스 코드를 테스트 할 때 사용한 파이프라인을 공개할 예정입니다.

- 준비 중

ComfyUI 감정 워크플로에서 마스크 사용은 선택 사항입니다. 사용할 때는 원본 이미지와 별도의 `LoadImage` 노드에서 파일명을 `__RIM_INPAINT_MASK__`로 지정하고, 선택 창에서 영역을 그리세요. 마스크 PNG는 선택 영역의 알파가 투명하며 `LoadImage`의 `MASK` 출력에서 그 영역이 1이 됩니다. 워크플로에 이 토큰이 없으면 마스크를 업로드하지 않고 선택 영역 없이 진행할 수 있습니다. 텍스트 노드에서 추가 태그를 쓰려면 `__RIM_INPAINT_TAG__`를 넣으세요.

OpenAI와 Google은 인페인트 영역 선택 없이 Base 초상화와 설정된 참조 이미지를 바탕으로 감정 초상화를 생성합니다. 마스크를 전송하거나 로컬에서 합성하지 않고 Provider 응답을 그대로 저장합니다.

## 6. 다른 Provider 지원에 대해서

---

- **다른 Provider에 대한 공식 지원은 정말 많은 시간과 노력과 금전을 필요로 합니다.** Provider를 하나 추가할 때마다 제작자가 해당 서비스에 직접 가입하고, 사용 방법과 API를 익힌 뒤 테스트를 위한 비용까지 충전해야 하기 때문에 지속적으로 지원하기가 어렵습니다. 예를 들어서 제작자는 NAI 지원을 위해 10\$ 요금제를 쓰다가 개발 도중 전부 소진해서 25\$ 요금제를 구매하였습니다.

- 대신 **소스 코드를 MIT 라이선스로 공개**하고 있습니다. 출처와 라이선스 고지만 유지해 주신다면 소스 코드를 자유롭게 수정·빌드하여 **Steam 창작마당에 배포하셔도 됩니다.** 

- 다른 Provider를 추가할 수 있도록 확장을 고려해 프로젝트 구조를 설계했습니다. 제 기준에서는요.

---

## 7. 소스 코드 빌드

- 준비 중

## 8. 비용 참조

- Google, gemini-3.1-flash-lite-image: 1780원/34=52.35원/장

- Openai, gpt-image-2.5-sunburst, low quality: 460원/20장=23원/장

- NAI, nai-diffusion-4-5-full: Opus 월 25달러(한화 34,000) 기준 일정 이하 옵션시 무제한 추정, 그 이하 요금제면 위 둘에 비해 크게 불리한 것으로 추정.

- Comfy: Local Model.

- gpt-image-2.5-sunburst, low quality 위주로 지원이 들어간 이유는 위를 보면 알 수 있습니다. NAI가 가격으로 이기려면 1,360장의 정착민 초상화를 한달에 뽑아야합니다.

## 9. 향후 계획

- v1.2.0
  
  - LLM 로컬 모델, OpenAI 기반 정상 동작하는지 실험.

- v1.3.0
  
  - Race Provider 지원: 현재 "임피드"와 같이 들어가는 것을 자동으로 "임피드: 머리에 뿔이 난 악마 형태의 종족" 등으로 치환해서 프롬프트에 던짐.
  
  - Regenerate 기능 모델 분리: 앞의 Base 생성, Expressive 생성과 처리 로직이 다르기에 추후 NAI와 Comfy 성능을 올리기 위해서는 분리 필요.
  
  - Tag Extractor 기능 강화: 감정 생성용 Tag Extractor Prompt 로직을 개편. 예를 들어서 close eyes가 있으면 다른 eye color 등 feature를 억제하도록.

- v1.4.0
  
  - 사용자 가이드 기능 강화.
  
  - 모드 옵션 탭을 카테고리별로 묶어서 정리할 수 있도록 반영.
  
  - Comfy와 관련된 지원 검토.
