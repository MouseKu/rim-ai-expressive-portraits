## 1. RIM AI Expressive Portraits

---

<img src="Resources/About/Preview.jpg" alt="RIM AI Expressive Portraits preview">

- **RIM AI Expressive Portraits** is a mod that generates AI portraits from a colonist's **in-game appearance (sprite), health, age, gender, and other information**.

![](.img/generation.webp)

- It combines that information to generate portraits with Gemini, OpenAI, or ComfyUI.

![](.img/shfit.webp)

- It can also **show a different portrait as the colonist's state changes**.

## 2. Requirements

---

- RimWorld 1.5 or 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- An image generation provider:
  - OpenAI API key
  - Google AI API key
  - NovelAI (NAI)
  - Local model based on the OpenAI API standard

## 3. Features

---

### 3-1. Generate a Base Portrait

![](.img/main-v2.jpg)

- A button in the colonist's action list opens the AI portrait menu.
- All colonists share the Base prompt. Enter details specific to a colonist in **Extra**.
- To generate a portrait:
  - Click **Generate new Base**.
  - When generation finishes, the portrait is applied automatically.
  - You can change it later by double-clicking a portrait in the gallery or clicking **Confirm**.
- The gallery also lets you regenerate a selected image, remove its background, delete it, open its image folder, and select multiple images with Ctrl-click or Shift-click.

### 3-2. Generate Expression Portraits

You can generate several state-specific expression portraits from a Base portrait.

![](.img/expression-multi.png)

- Several expressions are available, but you must enable them in the options to see them. By default, only **Low** (low mood) and **High** (high mood) are enabled.
- To generate expressions:
  - Select a Base image.
  - Click **Generation emotions from Base**.
  - Check the expressions you want and start generation.
  - Generated expressions are applied automatically. Use the arrow keys to browse them, then double-click or click **Confirm** to apply the expression currently shown.

![](.img/make-expression2.png)

### 3-3. Remove the Background

![](.img/bg-remove-v2.jpg)

- Left-click to remove the background.
  - **Background sensitivity:** Higher values remove colors more aggressively.
  - **Edge erosion:** Removes additional pixels along edges touching the background color.
  - **Undo:** Reverts the last mouse click.
  - **Auto remove enclosed areas:** Also removes enclosed areas within the character automatically.

![](.img/remove-bg-v2.webp)

### 3-4. OpenAI

![](.img/open-ai-examples1.png)

- Among the models tested, Sunburst at low quality offers the best quality for its price. Medium and high quality cost considerably more.
- Transparent backgrounds are supported, but the option is disabled because of consistency issues.

### 3-5. Google Gemini

![](.img/gemini-examples.png)

- Transparent backgrounds are not supported.
- `gemini-3.1-flash-lite-image` costs about twice as much per image as Sunburst at low quality.

### 3-6. Local Model

![](.img/comfy-example.png)

- Setting up a local model takes a great deal of time and effort. The author has not yet achieved the desired image quality with it.
- The mod follows the OpenAI API standard, and a compatible [Comfy Custom Node](./Comfy/README.md) is provided.

### 3-7. NovelAI (NAI)

- NAI requires a subscription. V4.5 is stable, while V5 currently has very limited functionality.
- Compared with OpenAI and Google, NAI behaves more like a dedicated image generation model and is less flexible.
- **Inpaint** and **Tag Extractor** help with that limitation.
- **Inpaint:** Drag to select the area to change. A higher **Inpaint Strength** changes it more substantially. Expressions such as Sleep, which require the eyes to close completely, need a higher strength. You can edit the positive and negative tags directly to adjust the result.

![Inpaint area selection](.img/inpaint.jpg)

- **Tag Extractor:** Uses an LLM to recognize an input image and turn it into positive and negative tags.
- NAI recognizes modded races reasonably well, but often struggles with simpler vanilla characters. Enabling Tag Extractor can help to some extent.

![Tag Extractor](.img/tag-extractor.jpg)

## 4. Options

---

### 4-1. Basic Options

![](.img/base-settings1.png)

- Choose OpenAI, Gemini, Comfy (Local Model), or NAI as the provider.
- The default is OpenAI with `gpt-image-2.5-sunburst`; enter your API key to use it.

![](.img/base-settings2.png)

- Choose which information is included in the prompt automatically. By default, generation uses both the character's in-game image and text information such as gender and age.

### 4-2. Expression Options

![](.img/expression-settings1.png)

- You can switch portraits according to a colonist's state. There are nine states in total; enabling them all may be expensive. You can also leave this feature off.

![](.img/expression-settings2.png)

- Base portrait generation, expression generation, and portrait regeneration can each use different models.

### 4-3. Style Reference

- Select a folder to use its images as visual style references.
  - One or two images are enough; avoid adding more than four. NAI can use only one. The default includes one or two images.
  - Keep image resolution small. The default style images are 256 × 256 pixels.

### 4-4. Other Options

![](.img/position-settings.png)

- Adjust the portrait's size and position.

## 5. Installation

---

- Subscribe to the mod on the Steam Workshop.

## 6. Support for Other Providers

---

- **Officially supporting other providers requires substantial time, effort, and money.** For each provider, the author has to sign up for the service, learn its API, and pay for testing. For example, while adding NAI support, the author used up a $10 plan and then bought a $25 plan to continue development.
- The **source code is available under the MIT License**. If you retain the attribution and license notice, you may modify and build it and **distribute your version on the Steam Workshop**.

---

## 7. Cost Reference

- Google, `gemini-3.1-flash-lite-image`: ₩1,780 for 34 images, or about ₩52.35 per image.
- OpenAI, `gpt-image-2.5-sunburst` at low quality: ₩460 for 20 images, or ₩23 per image.
- NAI, `nai-diffusion-4-5-full`: The Opus plan costs $25 per month (about ₩34,000). Generation is estimated to be unlimited when options stay below certain limits; lower-tier plans are estimated to be much less cost-effective than the two providers above.
- Comfy: Local Model.
- These estimates explain the focus on `gpt-image-2.5-sunburst` at low quality. NAI would need to generate about 1,360 colonist portraits per month to beat it on price.
