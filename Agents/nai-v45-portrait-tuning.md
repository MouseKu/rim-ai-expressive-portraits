# NovelAI Portrait Generation — Consolidated Findings & Working Baseline

## 1. Goal

Generate consistent RimWorld character portraits while preserving the source character's recognizable design.

### Primary requirements

- Preserve identity, species traits, anatomy, hairstyle, hair color, eye color, clothing, accessories, and important colors.

- Produce a consistent medium-bust portrait.

- Keep the full head and important protruding features such as ears, horns, or headwear visible.

- Keep both shoulders and enough upper chest visible for clothing to remain recognizable.

- Continue the torso beyond the bottom edge rather than creating a floating bust.

- Use the supplied Style Reference for drawing style when available.

- Use a flat, removable background for V4.5.

- Prefer consistency and character recognition over decorative detail.

---

# 2. Current Recommended Architecture

The most reliable approach is to separate responsibilities instead of asking one prompt or reference image to control everything.

```text
Character Sprite
   |
   +--> Character Precise Reference
   |       -> visual identity / resemblance
   |
   +--> Sprite-to-Tag Extractor
           -> semantic interpretation of visible features
                    |
                    v
              Positive Prompt

Style Reference
   -> drawing style only

Base Prompt
   -> composition / framing / background

Expression Prompt
   -> temporary facial expression or state
```

### Responsibility split

| Component           | Responsibility                                                 |
| ------------------- | -------------------------------------------------------------- |
| Character Reference | Visual identity and resemblance                                |
| Sprite Tags         | Explicit semantic character attributes                         |
| Style Reference     | Linework, facial stylization, shading, rendering, detail level |
| Base Prompt         | Composition, framing, camera direction, background             |
| Expression Prompt   | Temporary expression/state                                     |

This separation is important. Character identity, style, composition, and expression should not compete inside one large prompt.

---

# 3. V4.5 Working Baseline

Use `nai-diffusion-4-5-full`.

```json
{
  "width": 832,
  "height": 832,
  "steps": 23,
  "scale": 5,
  "sampler": "k_euler_ancestral",
  "noise_schedule": "karras"
}
```

A historically reliable Character Reference baseline was:

```text
strength: 0.65
fidelity: 0.8
```

These values are starting points, not universal optimums.

For the Character + Style Precise Reference workflow, the strongest style result observed used approximately:

```text
Style Reference:
  strength: 0.75
  fidelity: 0.5
```

Experiment `036` is the current style baseline, but it has **not** been proven to be the universal identity-preservation sweet spot.

---

# 4. Most Important Finding: Seed Variance Is Large

Identical settings can produce substantially different:

- composition,

- character scale,

- clothing,

- colors,

- hair detail,

- line texture,

- shading,

- background texture.

Therefore:

> Never evaluate a prompt or parameter change using different seeds.

### Controlled tuning procedure

```text
1. Fix the seed.
2. Change exactly one variable.
3. Compare outputs.
4. Reject changes that damage another important property.
5. Only after a same-seed improvement is confirmed, test multiple random seeds.
```

A better image from a different seed is not evidence that the changed setting is better.

Production settings must ultimately be tested across many random seeds.

---

# 5. Character Reference Findings

Character Reference is useful primarily for **identity preservation**, not composition control.

Increasing reference strength generally improves preservation of major features such as:

- hair color,

- ear color,

- species traits,

- clothing,

- scarf/apron,

- major accessories.

However, reducing Character Reference strength does **not** reliably provide more composition freedom.

For example, changing approximately:

```text
0.65 -> 0.55
```

had little reliable effect on head-to-canvas ratio.

Even very high Character Reference strength/fidelity did not reliably preserve every small clothing or accessory detail.

### Key limitation

RimWorld sprites contain little semantic information.

A tiny cluster of pixels may represent:

- a headband,

- a scarf,

- a clothing layer,

- a hairstyle,

- an ear,

- another accessory.

Character Reference can preserve visual similarity without correctly understanding what those pixels represent.

Increasing strength alone does not solve this ambiguity.

---

# 6. Sprite-to-Tag Extraction

Sprite-to-Tag extraction is the most important complement to Character Reference.

The two mechanisms solve different problems:

```text
Character Reference:
"Make the character visually resemble this image."

Sprite Tags:
"These pixels specifically represent black hair,
white mouse ears, a gray scarf, a white apron, etc."
```

Using both is substantially more reliable than Character Reference alone.

## Extract

Only persistent visible character attributes:

- species / anatomy,

- gender presentation when visually clear,

- broad apparent age,

- skin color,

- hairstyle,

- hair color,

- eye color when visible,

- ears / horns / antennae / tails,

- headwear,

- clothing type,

- clothing colors,

- accessories,

- distinctive visible features.

## Do not extract

- art style,

- background,

- camera angle,

- image quality,

- rendering technique,

- temporary expression,

- uncertain/speculative details.

Output should remain concise and suitable for NovelAI prompting.

---

# 7. Important Difference Between Earlier and Production Tests

Some earlier V4.5 experiments produced excellent identity preservation because the prompt explicitly described character-specific features such as:

```text
black hair
white mouse ears
gray scarf
white apron
patterned black headband
```

Those results should **not** be interpreted as proof that Character Reference alone can achieve the same result.

When character-specific descriptions were removed and the model had to infer everything from the sprite, identity preservation became significantly less reliable.

This is another reason to use an automatic Sprite-to-Tag extractor in production.

---

# 8. Style Reference Findings

Character + Style Precise Reference is a promising architecture because it separates:

```text
Character Reference
    -> who the character is

Style Reference
    -> how the character is drawn
```

Style Reference produced meaningful improvements in:

- linework,

- shading,

- rendering style,

- overall hand-drawn appearance.

Observed Style Reference strengths:

```text
0.25 -> too weak
0.50 -> clearly noticeable
0.60 -> useful but weaker
0.75 -> strongest preferred style result (036)
```

The Style Reference's background did not necessarily transfer into the generated background.

## Important limitation

Style Reference is **not identity-preserving**.

At stronger settings it can alter:

- clothing,

- clothing colors,

- accessories,

- proportions,

- other character-specific details.

For example, experiment `036` preserved several major features while altering clothing details. Another seed with the same settings changed the outfit even more.

Therefore:

> Style Reference should be treated as a style source, not as a character-preservation mechanism.

---

# 9. Do Not Over-Specify Style in the Base Prompt

When Style Reference is active, the text prompt should not independently redefine the entire art style.

Avoid simultaneously specifying large amounts of:

- facial geometry,

- eye proportions,

- hair rendering rules,

- exact line behavior,

- exact shading behavior,

- detailed stylistic anatomy.

This can make text conditioning compete with the Style Reference.

### Preferred approach

Let Style Reference control:

```text
line quality
facial stylization
hair rendering
shading
color treatment
detail level
```

Let Character Reference + Sprite Tags control:

```text
identity
species
anatomy
hair design
clothing
accessories
original colors
```

Let Base Prompt control:

```text
composition
framing
camera direction
crop
background
```

---

# 10. Composition Findings

Composition is difficult to control precisely through text alone.

## Too little crop guidance

Can produce:

- excessive torso,

- visible waist,

- too much clothing,

- character too small in the canvas.

## Too much crop guidance

Can produce:

- oversized head,

- oversized ears,

- insufficient visible clothing,

- close-up/headshot framing,

- background artifacts.

The most reliable target is an intermediate medium bust.

### Recommended composition intent

```text
Use a consistent three-quarter-view medium bust composition.

Keep the face at approximately the same position and scale across portraits,
regardless of hairstyle, ears, horns, or headwear.

Keep both shoulders clearly visible.

Show enough upper chest for important clothing details to remain readable.

The torso should continue beyond and be cropped by the bottom edge.

Avoid close-up headshots, waist-up compositions, and floating busts.
```

Avoid instructions such as:

```text
center the character vertically
```

because unusual ears, horns, hair, or headwear may cause the model to center the total silhouette rather than the face.

---

# 11. Background Findings

## V4.5

V4.5 did not reliably produce true alpha transparency.

Therefore, the practical workflow is:

```text
Generate flat background
        ->
Remove background in post-processing
```

Useful prompt requirements:

```text
plain matte background
uniform flat background
no texture
no gradient
no pattern
no halo
```

Common failures include:

- grid texture,

- canvas/linen texture,

- scribbles,

- halftone patterns,

- subtle gradients,

- white halos.

Background quality should be evaluated independently from character quality.

A visually attractive background can still be unsuitable for automated removal.

### Important observation

Input preprocessing had a larger effect on background uniformity than prompt wording in some experiments.

Compositing the original sprite onto a flat background before Img2Img produced extremely uniform backgrounds.

---

# 12. Texture and Rendering Findings

Strong `"hand-drawn"` or traditional-media wording sometimes improved:

- natural line variation,

- clothing detail,

- shadow variation.

But it also frequently introduced:

- background texture,

- grid-like patterns,

- fine scribbles,

- hatching,

- accessory changes,

- composition drift.

Likewise, adding increasingly detailed instructions for:

- narrow eyes,

- stronger outlines,

- additional shadows,

- overlapping hair locks,

- facial geometry,

did not reliably improve the final result.

General rule:

> Once a stable style is found, adding more stylistic instructions often reduces stability instead of improving it.

---

# 13. Parameters That Were Less Important Than Expected

## Steps

Increasing:

```text
23 -> 28 / 30
```

did not reliably improve:

- composition,

- identity,

- background texture,

- portrait scale.

`23 steps` remains a good practical baseline.

## Quality Toggle

Controlled comparisons showed effectively negligible differences.

Do not expect Quality Toggle to solve identity or detail problems.

## Scale

Changing scale did not solve the fundamental identity/composition trade-off.

## Reference strength for composition

Lowering Character Reference strength is not an effective composition-control mechanism.

---

# 14. Failed or Low-Value Approaches

The following approaches did not solve the core problem and generally do not need to be repeated:

- Repeatedly tuning Character Reference strength/fidelity alone.

- Increasing steps beyond the working baseline.

- Using Quality Toggle to improve detail.

- Strong frame-filling/crop instructions.

- Excessive `"hand-drawn"` descriptors.

- Additional narrow-eye / outline / shadow instructions.

- Additional hair-overlap instructions.

- Repeatedly adding more style descriptors.

- Cropping Character Reference to the upper body.

- Preserving RGBA alpha in Character Reference.

- Creating a reference sheet containing the full sprite plus enlarged face.

- Removing the Base Prompt completely.

- Using V4.5 Curated instead of Full.

- Converting Style Reference to grayscale.

- Splitting Style Reference into small style patches.

- Assuming one successful seed proves a configuration is robust.

V4.5 Full remains preferable to Curated based on these experiments.

---

# 15. Historical Reference Points

## Experiment 019

Best early V4.5 result using explicit character descriptions.

Successfully combined:

- recognizable character colors,

- patterned black headband,

- mouse ears,

- scarf,

- apron,

- chest-level composition,

- nearly uniform background.

Its importance is not that its character-specific prompt should be copied globally.

The important lesson is that its relatively simple prompt structure was more stable than later increasingly complicated variants.

## Experiment 036

Best Style Reference result from the later generalized BaseProvider tests.

Strengths:

- improved linework,

- improved shading,

- stronger target style,

- retained several major character traits.

Weakness:

- clothing/accessory identity could still drift,

- results changed significantly across seeds.

Treat `036` as the current **style baseline**, not as a proven universal production configuration.

---

# 16. Recommended Base Prompt Architecture

Keep the production prompt modular.

## A. Base Prompt

Global requirements only:

- portrait type,

- composition,

- framing,

- background,

- minimal global rendering constraints.

## B. Sprite Tags

Generated automatically per character.

Persistent visible attributes only.

## C. Character Reference

Supplied separately through the API.

Provides visual identity information difficult to encode completely in tags.

## D. Style Reference

Supplied separately.

Controls drawing style only.

## E. Expression Prompt

Added when generating emotional/state variants.

It should change the requested expression or state without redefining the character.

### Conceptual final prompt

```text
Final Positive Prompt
    =
Base Prompt
+ Sprite Tags
+ Requested Expression
```

Character Reference and Style Reference remain separate API inputs.

---

# 17. Recommended V4.5 Base Prompt

```text
Create a square character portrait following the supplied image references and their assigned roles.

Use the style reference as the primary authority for drawing style, including line quality, facial stylization, hair rendering, shading, color treatment, and level of detail.

Preserve the character reference's identity, anatomy, species traits, hairstyle, hair color, clothing, accessories, colors, and distinctive features. Do not copy character-specific features from the style reference.

Use a consistent three-quarter-view medium bust composition. Keep the face at approximately the same position and scale across portraits regardless of hairstyle, ears, horns, or headwear. Keep both shoulders clearly visible and show enough upper chest for important clothing details to remain readable. The torso should continue beyond and be cropped by the bottom edge. Avoid close-up headshots, waist-up framing, and floating busts.

Use a uniform removable background in the requested background color. No scene, objects, text, border, gradient, pattern, halo, or background texture.
```

Avoid adding detailed facial proportions or hair-rendering instructions unless controlled testing proves they are necessary.

---

# 18. V5 Findings

V5 behaves differently enough that it should be treated as a separate workflow.

## Main advantage

V5 can generate genuine alpha transparency.

Prompts such as:

```text
transparent background
```

successfully produced images containing large amounts of fully transparent pixels.

This can eliminate background-removal post-processing.

## Main disadvantage

V5 lacks the same Precise Character Reference workflow used by V4.5, so Image2Image was used instead.

This couples two different requirements to one `strength` value:

```text
Low strength
    -> good source preservation
    -> source composition/rendering also preserved

High strength
    -> stronger redesign and portrait composition
    -> weaker identity preservation
```

This is the central V5 trade-off.

---

# 19. V5 Strength Findings

## `0.65`

Too strongly follows the original sprite composition and rendering.

## `0.75–0.85`

Begins producing a proper portrait, but can still cause:

- oversized ears,

- simplified face,

- reduced clothing visibility.

## `0.95`

Produced the strongest portrait reconstruction in the tests.

A successful result preserved:

- headband,

- ears,

- scarf,

- apron,

- transparent background.

However, different seeds could alter:

- scarf color,

- eye color,

- face,

- composition.

Therefore:

> V5 at high strength can produce excellent individual images, but identity stability is weaker than the preferred V4.5 workflow.

---

# 20. V5 Prompt Findings

## Avoid `visual novel art`

It increased eye detail but also caused significant character drift, including ear shape and clothing/composition changes.

## Avoid simple negatives such as `large ears`

Negative prompting can change the **type** of ear rather than simply reducing its size.

Prefer positive semantic descriptions:

```text
round mouse ears
white fur rim
pink inner ear
```

Describe the desired anatomy rather than only suppressing the undesired result.

## Warm multi-stage shading

Explicit restrained warm shading produced useful improvements on:

- hair,

- face,

- scarf,

- clothing.

However, it could also increase color drift.

---

# 21. V4.5 → V5 Two-Stage Workflow

A two-stage approach was tested:

```text
Original Sprite
    ->
V4.5 portrait
    ->
V5 Image2Image
```

### Advantages

- V4.5 establishes a better face and composition first.

- More stable than sending the raw sprite directly to V5 in some tests.

- V5 can add genuine transparency.

### Problems

At lower V5 strength:

- V4.5 background remains.

- transparency may fail.

At higher strength:

- transparency works,

- but V5 begins modifying the face, ears, clothing, and composition again.

This workflow is viable but requires two generations and does not eliminate identity drift.

---

# 22. V4.5 vs V5

| Property                 | V4.5                                | V5                                          |
| ------------------------ | ----------------------------------- | ------------------------------------------- |
| Character Reference      | Strong Precise Reference workflow   | More limited for this workflow              |
| Identity preservation    | Better                              | Less stable at high Img2Img strength        |
| Style transformation     | Strong                              | Strong                                      |
| Major composition change | Good                                | Requires higher Img2Img strength            |
| True alpha transparency  | Poor/unreliable                     | Strong                                      |
| Seed drift               | Present                             | Significant                                 |
| Recommended use          | Main production portrait generation | Transparency or experimental alternate path |

### Current practical conclusion

For identity-sensitive portrait generation:

```text
V4.5
+ Character Reference
+ Sprite Tags
+ Style Reference
+ Flat removable background
```

is currently the more robust architecture.

V5's strongest advantage is genuine transparency, but its Image2Image workflow makes major reconstruction and identity preservation compete with each other.

---

# 23. Production Validation Requirements

Do not optimize against one character or one lucky seed.

The current architecture should be tested across:

```text
multiple characters
multiple species
multiple hair colors
multiple clothing types
multiple accessories
multiple seeds
```

Evaluate each result independently for:

```text
species traits
anatomy
hair style
hair color
eye color
head accessories
neck accessories
clothing type
clothing color
composition
style consistency
background uniformity
```

Pay particular attention to whether Style Reference overwrites clothing or character colors.

---

# 24. Recommended Tuning Procedure

For future experiments:

1. Start from the current known-good baseline.

2. Fix the seed.

3. Change exactly one variable.

4. Compare identity, composition, style, clothing, colors, and background independently.

5. Reject changes that improve one minor feature while damaging important identity information.

6. Confirm improvements across several random seeds.

7. Test substantially different character anatomies.

8. Only then promote the configuration to the production baseline.

Do not accumulate prompt clauses simply because an individual generation failed.

---

# 25. Current Working Conclusions

1. **Character Reference alone is insufficient for reliably interpreting tiny RimWorld sprites.**

2. **Character Reference + Sprite Tags is substantially more useful than either mechanism alone.**

3. **Style Reference successfully transfers drawing style, but can also alter clothing and colors.**

4. **Style Reference should control style, not character identity or composition.**

5. **Base Prompt should explicitly control composition and framing.**

6. **Fixed-seed A/B testing is mandatory during tuning.**

7. **Production stability must be evaluated across random seeds and diverse characters.**

8. **More prompt detail is not automatically better; excessive style instructions often reduce stability.**

9. **V4.5 should use a flat removable background rather than attempting true transparency.**

10. **V5 provides genuine transparency but currently has a stronger identity-vs-reconstruction trade-off.**

11. **The production system should optimize for robustness across many pawns rather than maximum similarity on one lucky generation.**

---

# 26. Short Agent Summary

```text
Preferred production path:
V4.5 Full
+ Character Precise Reference
+ Sprite-to-Tag attributes
+ Style Precise Reference
+ concise Base Prompt
+ flat removable background

Baseline:
832x832
23 steps
scale 5
Euler Ancestral
Karras

Historical Character Reference baseline:
strength 0.65
fidelity 0.8

Current Style Reference baseline:
strength 0.75
fidelity 0.5
(036; style baseline only, not proven universal identity optimum)

Rules:
- Fix seed for A/B tests.
- Change one variable at a time.
- Do not use reference strength to control composition.
- Do not overload Base Prompt with style details when Style Reference exists.
- Use Sprite Tags to resolve semantic ambiguity in low-resolution sprites.
- Keep composition controlled by Base Prompt.
- Treat Style Reference as style-only; watch for clothing/color drift.
- V4.5: use flat background + post-processing.
- V5: true transparency works, but identity is less stable at reconstruction strengths.
- Validate across many characters and random seeds before changing production defaults.
```
