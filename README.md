## 1. RIM AI Expressive Portraits

---

<img src="Resources/About/Preview.png" title="" alt="Rim AI Expressive Portraits preview" data-align="center">

- **RIM AI Expressive Portraits** is a mod that generates AI portraits based on information about your colonists, including their **in-game appearance (sprite), health conditions, age, and gender**.

![](.img/generation.webp)

- It combines information such as a colonist's **in-game appearance, health conditions, age, and gender** to generate portraits through Gemini, OpenAI, or ComfyUI.

![](.img/shfit.webp)

- It can also **display different portraits depending on the colonist's current state.**

## 2. Requirements

---

- RimWorld 1.5 or 1.6

- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

- An image-generation provider:

  - OpenAI API key
  - Google AI API key
  - Local ComfyUI server

## 3. Features

---

### 3-1. Generating a Base Portrait

![](.img/make-base.png)

- A button that opens the AI portrait menu is available in the colonist's action list.

- The first time a colonist is accessed, the default prompt is loaded from the mod settings. You can edit the base prompt for each colonist or enter any additional information you want to provide in the Extra field.

- To generate a portrait:

  - Click **Generate new Base**.

  - Wait for generation to finish.

  - Click the generated portrait, then click **Confirm emotion assignment**.

- Other supported features include:

  - Regenerating the selected image
  - Removing the background color
  - Deleting an image
  - Opening the image folder

### 3-2. Generating Expression Portraits

![](.img/make-expression1.png)

- You can generate multiple portraits for different emotional states from a generated portrait.

![](.img/expression-multi.png)

- Several expressions are available, but they must be enabled in the settings before they are shown. By default, only Low (low mood) and High (high mood) are enabled.

- To generate expression portraits:

  - Select a Base image.

  - Click **Generation emotions from Base**.

  - Select only the expressions you want, then generate them.

![](.img/make-expression2.png)

### 3-3. Removing the Background

![](.img/background-remove.png)

- OpenAI can be instructed to generate an image with a transparent background, but some providers do not support this. In the background-removal options, you can use the eyedropper to select the background color and remove it.

- Click **Align** to align the image to the bottom edge.

### 3-4. OpenAI

![](.img/open-ai-examples1.png)

- OpenAI's Sunburst model at low quality is the configuration that has been tested the most. It supports transparent backgrounds and, among the models tested, offers relatively good quality for its price. Medium and high quality are considerably more expensive.

- In the author's personal testing, generating five images with OpenAI Sunburst at low quality cost $0.08.

- However, the exact cost per image is uncertain, so please keep a close eye on the usage charges for your API key.

### 3-5. Google Gemini

![](.img/gemini-examples.png)

- It does not support transparent backgrounds. Because generating a consistently transparent background was difficult, it lost out to OpenAI during testing and is no longer the default option.

- The image quality is not poor, but results currently tend to deviate from the prompt slightly.

### 3-6. Comfy (Local Model)

![](.img/comfy-example.png)

- Setting up a local model requires a great deal of time and effort, especially if you want consistent results. The author was unable to achieve the desired results.

- The pipeline used for testing will be published in the future.

## 4. Options

---

### 4-1. Basic Options

![](.img/base-settings1.png)

- You can choose between OpenAI, Gemini, and Comfy (Local Model) as the provider.

- The default configuration uses OpenAI with `gpt-image-2.5-sunburst`; you only need to enter an API key.

- **WIP**: Style Reference is intended to let you add multiple reference images, but its practical use is still being worked out. The prompt will probably also need to be adjusted to account for multiple input images.

![](.img/base-settings2.png)

- You can choose which information is automatically included in the prompt. By default, images are generated using both the character's in-game appearance and text information such as gender and age.

### 4-2. Expression Options

![](.img/expression-settings1.png)

- Portraits can change according to a colonist's state. There are nine states in total, but enabling all of them may become quite expensive. You can use the mod with this feature disabled.

![](.img/expression-settings2.png)

- You can also select a separate provider for expression generation. Both portrait types may use the same provider, or you can mix different providers.

### 4-3. Other Options

![](.img/position-settings.png)

- You can adjust the portrait's size and position.

## 4. Installation

---

- Coming soon.

## 5. Comfy Setup Tips

---

- **Setting up a local model requires a considerable amount of time and effort.** Maintaining consistency between portraits for different expressions makes it even more difficult, so using a local model is not recommended unless you are familiar with this field.

- For reference, the pipeline used by the author while testing the source code will be published in the future.

- Coming soon.

## 6. Support for Other Providers

---

- **There are currently no plans to officially support other providers.** Adding a provider requires the author to sign up for the service, learn how to use its API, and pay for testing credits, which makes ongoing support difficult.

- Instead, the **source code is available under the MIT License**. As long as you retain the attribution and license notice, you may freely modify and build the source code and **distribute it on the Steam Workshop.**

- The project structure was also designed with extensibility in mind so that other providers can be added—at least, according to the author's standards.

---

## 7. Building from Source

- Coming soon.
