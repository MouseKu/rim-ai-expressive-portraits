전체 설명: https://github.com/MouseKu/rim-ai-expressive-portraits/blob/main/README.md

![open-ai-examples1.png](.img/open-ai-examples1.png)

- AI(OpenAI, Google, NAI, LocalModel)을 사용해서 정착민의 초상화를 자동으로 생성하고 화면에 표시합니다.

- 정착민의 게임 내 외형, 유전자, 건강 상태, 나이, 성별 등의 정보를 프롬프트로 조합해서 생성할 수 있습니다.

- 정착민의 현재 상태에 따라서 서로 다른 초상화를 표시할 수도 있습니다.

![main-v2.jpg](.img/main-v2.jpg)

초상화 생성 방법:

1. 모드 옵션에서 원하는 공급자를 고르고 API 키를 넣어주세요.

2. 기즈모에서 초상화 버튼을 누르세요.

3. Generate new Base를 클릭하고, 기다리세요. 이후 자동적용 됩니다.

4. 다른 것으로 바꾸고 싶다면 초상화 패널에서 더블 클릭이나 Confirm을 누르면 됩니다.
- 배경 제거, 이미지 회전이나 정렬 등 편의성 기능을 Edit 버튼 아래에서 제공하고 있습니다.

![bg-remove-v2.jpg](.img/bg-remove-v2.jpg)

- 상태별 초상화 생성 방법
1. Generation emotions from Base를 클릭합니다.

2. 원하는 표정을 체크한 뒤 기다립니다. 이후 적용됩니다. 초상화 패널에서 방향키를 통해서 생성된 표정들을 확인할 수 있으며, 원하는 이미지를 더블 클릭하거나 Confirm을 누르면 보고 있는 표정에 다른 이미지가 적용됩니다.

---

- OpenAI의 Sunburst 모델을 Low 품질로 사용하는 설정이 가장 많이 테스트되었습니다. 테스트 모델 중 가격 대비 비교적 좋은 품질을 제공합니다. Medium과 High 품질은 비용이 상당히 높습니다.  
  
  - Google, gemini-3.1-flash-lite-image:  
    이미지 34장당 약 1,760원, 장당 약 52원  
  
  - OpenAI, gpt-image-2.5-sunburst (Low):  
    이미지 20장당 약 460원, 장당 약 23원  
  
  - NovelAI, nai-diffusion-4-5-full:  
    월 약 33,600원의 Opus 플랜은 특정 옵션 제한 이하에서 무제한 생성이 가능한 것으로 추정됩니다. 

- 다만 실제 이미지 한 장당 비용은 변동될 수 있으므로, API 키의 사용량 및 실제 청구 금액을 반드시 주의 깊게 확인해주세요.

---

알려진 문제:

- 간혹 게임에서 스프라이트는 정상 표시되지만, 메타 데이터(게임 내 정보)가 실제 외형과 일치 하지 않는 경우가 있습니다. 대표적으로 초상화가 대머리로 나타나는 문제입니다. 이런 문제가 발생하면 옵션에서 Base Provider로 가서 해당 정보 입력을 비활성화 하거나, Extra에 문제 사항을 AI가 피하도록 지시해주세요.

- 투명 배경은 AI가 일관성 있게 생성하지 못합니다. 그나마 OpenAI가 기능을 제공하지만(background:transparent) 일관성이 조금 모자랍니다.

- NAI V5는 현재 지원이 어렵습니다. 현재 V4.5 대비 V5가 지원하지 않는 기능이 너무 많아서 여러 방법을 시도했지만, 일관성 있는 방법을 찾지 못하였습니다. 향후 API가 업데이트 된다면 검토 예정입니다.

---

v1.4.0 릴리즈

- 이제 OpenAI 규격 기반의 로컬 모델이 지원됩니다. Comfy의 경우 Custom Node도 지원 중입니다. https://github.com/MouseKu/rim-ai-expressive-portraits/tree/main/Comfy

- 편의성 및 여러 기능이 추가되었습니다.


