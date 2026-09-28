# NovelAI V4.5 Expression Inpainting — Consolidated Findings & Current Pipeline

## 1. Goal

Use NovelAI V4.5 Inpainting to modify a character's facial expression while preserving as much of the original portrait as possible.

The system should:

- preserve character identity,

- preserve hair, clothing, colors, and accessories,

- preserve untouched pixels exactly,

- modify only features required for the requested expression,

- avoid visible rectangular mask boundaries,

- avoid duplicated facial features,

- minimize color drift,

- support stronger transformations such as fully closing the eyes for Sleep.

The primary lesson from all experiments is:

> **Identity preservation is controlled more reliably by where generation is allowed than by endlessly tuning generation parameters.**

---

# 2. Current Core Architecture

The most important architectural finding is that three different regions must be treated independently.

```text
1. Context
   What the model sees around the feature.

2. Generation Mask
   Where NovelAI is allowed to regenerate pixels.

3. Final / Desired Mask
   Which pixels from the generated result are actually copied back.
```

These regions should **not** be identical.

The current preferred structure is:

```text
Original portrait
      |
      v
Face-centered crop
      |
      v
Resize crop to 832–1024
      |
      v
Generation Mask
(wider than the actual feature)
      |
      v
NovelAI V4.5 Inpaint
      |
      v
Generated result
      |
      v
Final Masks
(tightly covering only required features)
      |
      v
Small inward feather
      |
      v
Composite onto original portrait
```

Conceptually:

```text
Context > Generation Mask > Final Mask
```

This is the central design principle of the current pipeline.

---

# 3. Stable Generation Baseline

The generation parameters themselves were not the main source of improvement.

Current practical baseline:

```text
Model: NovelAI V4.5 Full Inpainting
Steps: 23
Scale: 5
Noise: 0.1
Samples: 1
add_original_image: true
```

Further tuning of steps, scale, and noise produced much less benefit than improving mask design.

---

# 4. Strength Findings

Strength controls a clear trade-off:

```text
Lower strength
    -> stronger source preservation
    -> weaker expression change

Higher strength
    -> stronger expression change
    -> more identity/color/shape drift
```

Observed Angry differences increased sharply with strength:

```text
0.4 -> average RGB change 9.38
0.6 -> average RGB change 15.68
0.7 -> average RGB change 28.95
```

The jump from `0.6` to `0.7` was particularly large.

## Current baseline

```text
General expressions:
    strength ≈ 0.6

Sleep:
    strength = 0.7

0.8+:
    generally avoid
```

Earlier experiments considered `0.35–0.45` for stronger preservation, but later tests showed that `0.6` provides a better general expression baseline when mask scope is properly controlled.

The preferred strategy is therefore:

> Do not reduce strength indefinitely to preserve identity. Use a sufficiently expressive strength and restrict the Final Mask instead.

---

# 5. Why Sleep Requires Special Handling

Sleep is fundamentally different from many other expressions.

For Angry, Distressed, Low, etc., the original eyes may remain recognizable.

For Sleep:

```text
open eye
    ->
closed eyelid
```

is a significant structural change.

Observed results:

```text
strength 0.60 -> eyes frequently remain open
strength 0.65 -> often incomplete
strength 0.70 -> reliably capable of closing eyes
strength 0.80 -> stronger change but more artifacts
```

Therefore:

```text
Sleep strength = 0.7
```

remains the current working baseline.

Sleep should have its own mask strategy and should not simply reuse the general-expression pipeline unchanged.

---

# 6. Mask Scope Is More Important Than Prompt Complexity

Early full-face masks produced strong expressions but also regenerated:

- iris color,

- eyebrows,

- skin,

- hair,

- clothing,

- face structure.

A mouth-only mask preserved the rest of the character almost perfectly, but expressions became weaker because the model could not modify the eyes or eyebrows.

This established an important rule:

> Generate only the features that actually need to change.

For identity preservation:

```text
mask scope > negative prompt tricks
mask scope > minor strength tuning
mask scope > scale tuning
mask scope > noise tuning
```

---

# 7. Eye Color Preservation

Eye-color drift was one of the clearest examples of why prompt-based preservation is unreliable.

Observed behavior included:

```text
Original red/brown iris
    ->
yellow iris
```

Adding:

```text
yellow eyes
```

to the negative prompt did not restore the original color.

Instead, the model generated another incorrect color.

Adding multiple forbidden colors produced yet more alternative colors.

Changing:

- noise,

- scale,

- strength,

also failed to solve the problem reliably.

### Conclusion

Negative prompting can suppress a particular color, but it does not tell the model which original color must be preserved.

Therefore:

> If a feature must remain pixel-identical, the most reliable solution is to exclude it from the Final Mask.

Prompting should guide generation, not be treated as a pixel-preservation mechanism.

---

# 8. The Original Rectangular Boundary Problem

The first implementation used the same rectangle for:

```text
Generation Mask
+
Final Composite Mask
```

This produced visible rectangular seams.

Experiments confirmed that the gray rectangle was already present in the raw NovelAI response.

Therefore the artifact was **not primarily caused by**:

- local crop/paste,

- PNG encoding,

- missing `add_original_image`,

- final image saving.

NovelAI itself can generate color or structure discontinuities near the Generation Mask boundary.

---

# 9. Expanding the Generation Mask

An early major improvement was:

```text
Final Mask
    = original selected area

Generation Mask
    = Final Mask expanded outward
```

In the original 832×832 experiments, approximately 40px of expansion moved the generation boundary outside the area that would ultimately be copied.

This successfully removed many large rectangular seams.

The important idea was not the exact `40px` value.

It was:

> Generate a larger area than you intend to keep.

This became the foundation of the current `Generation Mask != Final Mask` architecture.

---

# 10. Two Different Types of Artifact

Later experiments revealed that "inpaint seams" actually consist of two different problems.

## A. Raw Generation Artifacts

Already present in NovelAI's output:

- gray skin,

- rectangular color regions,

- horizontal lines,

- duplicated structures,

- incorrect eyelids,

- changed hair or eyebrows.

## B. Composite Artifacts

Introduced when blending generated and original images:

- ghosting,

- doubled eye lines,

- blurred eyebrows,

- overlapping old and new features,

- visible transition boundaries.

These require different solutions.

```text
Bad raw generation
    -> regenerate / improve model input

Good raw generation + bad transition
    -> improve Final Mask / feather
```

No compositing algorithm can fully repair a fundamentally incorrect raw eye or incorrectly generated skin region.

---

# 11. Why the Old 24px Feather Was Too Large

The original compositing system used approximately:

```text
24px feather @ 832px
```

This worked reasonably for large facial rectangles.

However, it becomes excessive for small regions such as the eyes.

In one narrow Sleep mask, the 24px transition occupied approximately 70% of the selected area.

That means much of the result was neither:

```text
fully original
```

nor:

```text
fully generated
```

but a mixture of both.

When the old and new eye lines are spatially different, this creates:

- double eyelids,

- ghost eyes,

- blurred lines,

- muddy skin.

---

# 12. Feather Width Experiments

Stored raw responses were recomposited using:

```text
0px
4px
8px
12px
16px
24px
```

### 0px

Advantages:

- no ghosting.

Problems:

- hard visible boundary.

### 4–8px

Best overall behavior for small eye regions:

- limited ghosting,

- acceptable transition,

- better preservation of original surroundings.

### 12–16px

Old eye whites and lines increasingly reappear.

### 24px

Too large for narrow Sleep masks.

A large part of the eye region becomes a blend between incompatible old and new structures.

### Current recommendation

For small Sleep eye masks:

```text
4–8px inward feather
```

with approximately `8px` being a useful current working point.

This should **not** be applied globally to every expression.

---

# 13. Why Global 8px Feather Was Rejected

Reducing every expression to an 8px feather is not a universal solution.

Offline recomposition showed:

### Sleep

Smaller feather reduced blurred old-eye remnants.

### Angry / Low

Smaller feather could expose boundaries around:

- hair,

- clothing,

- other high-contrast structures.

Therefore:

> Feather width should depend on Final Mask size and feature type.

The system should not globally replace the old feather width with one constant value.

---

# 14. Final Mask Must Cover the New Feature Completely

A tempting solution was to keep shrinking the Final Mask.

This failed.

If the Final Mask becomes too small:

```text
new closed eye
+
remaining original open eye
```

can coexist.

This creates doubled structures.

Therefore:

> The Final Mask should be as small as possible, but large enough to completely replace the old feature with the new one.

The objective is not "smallest mask."

The objective is:

```text
smallest mask that fully contains the changed feature
```

---

# 15. Separate Left and Right Eye Final Masks

Using one large rectangle across both eyes unnecessarily replaces:

- skin between the eyes,

- eyebrows,

- hair,

- nearby facial regions.

A better Sleep structure is:

```text
Left Eye Final Mask
+
Right Eye Final Mask
```

This preserves the original pixels between and around the eyes.

Experiments showed this substantially reduced:

- rectangular color patches,

- unnecessary skin replacement,

- eyebrow drift,

- hair drift,

- old/new eye overlap.

### Current recommendation

For Sleep:

> Treat the two eyes as separate Final Masks.

---

# 16. Generation Mask and Final Masks Can Have Different Shapes

Importantly, separate Final Masks do **not** necessarily require separate NovelAI requests.

A useful structure is:

```text
        Large Generation Context
    +-----------------------------+
    |                             |
    |   [Left Eye]   [Right Eye]  |
    |                             |
    +-----------------------------+

          Generation Mask

        ↓ one NAI request ↓

     [Left Final]   [Right Final]
```

This allows NovelAI to understand both eyes and surrounding facial context while only copying the actual modified features into the final portrait.

One request is preferable to separate left/right generation requests unless future testing demonstrates otherwise.

---

# 17. Hard Generation Mask Is Preferred

Soft or feathered Generation Masks were tested.

Using a soft elliptical Generation Mask caused NovelAI to produce:

- elliptical gray boundaries,

- purple/gray transition artifacts.

Therefore:

```text
Hard Generation Mask
+
Separate feathered Final Mask
```

is currently more reliable than feathering the mask sent to NovelAI.

Do not try to solve compositing seams by making the Generation Mask itself soft.

---

# 18. Face Crop Was the Largest Generation-Side Improvement

One of the most important later discoveries was that the entire portrait should not necessarily be sent directly at its original scale.

For Sleep:

```text
Face region
    -> square crop
    -> enlarge
    -> NovelAI Inpaint
```

produced significantly better results.

A successful experiment used approximately:

```text
512×512 face crop
    ->
1024×1024 model input
```

The result:

- both eyes closed correctly,

- previous gray eye artifacts disappeared,

- Final Mask compositing became substantially cleaner.

The same approach succeeded on another base portrait.

---

# 19. Crop Matters More Than Simple Upscaling

Simply resizing:

```text
832×832 full portrait
    ->
1024×1024
```

did not reproduce the same improvement.

The eyes could still remain open.

But:

```text
face crop
    ->
1024×1024
```

worked.

Therefore the important variable is not simply pixel count.

The likely useful effect is:

> The face and eyes occupy a larger fraction of the model input.

This gives NovelAI more effective resolution and attention for the feature being modified.

---

# 20. 832 vs 1024 Face Crop

Both:

```text
face crop -> 832×832
```

and:

```text
face crop -> 1024×1024
```

successfully closed the eyes in tested cases.

Therefore:

```text
Face crop = important
1024 resolution = optional quality improvement
```

1024 produced somewhat smoother eye lines in the tested result, but approximately 1MP input is not required for the method to work.

Current practical range:

```text
832–1024
```

---

# 21. Current Recommended Sleep Pipeline

The latest preferred Sleep workflow is:

```text
1. Detect/select the face region.

2. Create a square crop around the face.

3. Resize the crop to approximately 832–1024.

4. Preserve sufficient surrounding facial context.

5. Create a Generation Mask wider than the eyes.

6. Send one NovelAI V4.5 Inpaint request.

   strength = 0.7
   steps = 23
   scale = 5
   noise = 0.1

7. Project the generated crop back into original-image coordinates.

8. Create separate Final Masks:

   - left eye
   - right eye

9. Ensure each Final Mask fully covers:
   - the original open eye,
   - the generated closed eye.

10. Apply approximately 4–8px inward feather.

11. Composite only those Final Mask regions.

12. Copy all pixels outside the Final Masks directly from the original.
```

Core rule:

```text
Context > Generation Mask > Final Mask
```

---

# 22. Current Runtime Direction for Sleep

The current implementation direction reflects the experimental results.

Sleep supports up to two eye selections:

```text
Left eye rectangle
Right eye rectangle
```

When two regions are selected:

```text
Final Mask
    = only the two selected eye regions

Generation Mask
    = larger contextual area covering both eyes
```

The space between the eyes remains original in the final portrait.

For dual-eye Sleep selection, feather width is reduced relative to the old large-mask behavior.

Approximately:

```text
~1% of short image dimension
832px -> ~8px

clamped approximately:
4–12px
```

Sleep also maintains its own strength:

```text
Sleep strength = 0.7
```

rather than relying on the general expression value.

---

# 23. General Expressions vs Sleep

The pipeline should not assume all expressions need identical masks.

## General expressions

Examples:

- Angry

- Distressed

- Pain

- Low

- Hot

- Cold

A general baseline remains:

```text
strength ≈ 0.6
```

But Final Mask scope should be limited to the features actually required.

For example:

```text
mouth only
eyes + eyebrows
mouth + eyebrows
eyes + mouth
```

depending on the expression.

## Sleep

Requires a structural eye replacement.

Therefore:

```text
strength = 0.7
face crop
two-eye Final Masks
small feather
```

is justified.

---

# 24. Expression-Specific Observations

Some states are easier for NovelAI to communicate than others.

## Strong physical states

Examples:

- Hot

- Cold

These have obvious visual signals such as:

- sweat,

- flushed skin,

- pale skin,

- lip-color changes.

They tend to remain recognizable even with constrained masks.

## Psychological expressions

Examples:

- Angry

- Pain

- Distressed

- Low

These rely more heavily on combinations of:

- eyebrows,

- eyelids,

- gaze,

- mouth shape.

Therefore there is a stronger trade-off between:

```text
small mask
```

and:

```text
strong readable expression
```

This suggests that expression-specific mask layouts are preferable to one universal face rectangle.

---

# 25. Prompt Preservation Has Limits

Prompts should still include preservation instructions such as:

```text
same identity
same face shape
same hairstyle
same hair color
same clothing
same colors
preserve original skin tone
preserve original eye color when visible
alter only the selected facial features
```

Sleep should emphasize:

```text
both eyes fully closed
closed eyelids
no visible irises
no visible pupils
relaxed sleeping expression
```

with negatives such as:

```text
open eyes
half-closed eyes
visible pupils
looking at viewer
```

However:

> Prompt instructions are guidance, not preservation guarantees.

If exact preservation is required, enforce it through the Final Mask.

---

# 26. Why Raw Result Inspection Matters

Before blaming compositing, inspect the raw NovelAI result.

Debugging should distinguish:

```text
raw_result
```

from:

```text
final_result
```

If the raw image already contains:

- gray skin,

- malformed eyelids,

- duplicated features,

- strong color drift,

then adjusting feather width cannot fully solve the problem.

Recommended debug outputs:

```text
source
crop
generation_mask
final_mask_left
final_mask_right
raw_result
projected_result
final_result
```

This makes it possible to determine whether a failure originates from:

```text
generation
```

or:

```text
compositing
```

---

# 27. Single Large Rectangle Fallback

If the user provides only one large rectangle and separate feature masks are unavailable, an older fallback can still be useful.

One tested configuration restored approximately:

```text
24px original region
+
16px transition
```

inside the Generation Mask boundary.

This produced acceptable results in limited samples.

However:

- ideal widths vary by character,

- too much restoration reveals the old feature,

- too little restoration leaves seams.

Therefore this should be treated as a fallback, not the preferred Sleep architecture.

---

# 28. Approaches That Do Not Need Further Repetition

The following experiments did not solve the core problem and generally should not be repeated without new evidence.

### Parameter tuning

- Increasing steps.

- Increasing scale.

- Reducing noise to preserve eye color.

- Using `strength >= 0.8` as a general solution.

- Using Sleep `strength = 0.6`.

### Prompt tricks

- Expanding negative prompts with every incorrect eye color.

- Assuming stronger preservation wording guarantees original pixels.

- Relying on skin-color wording alone to remove raw gray artifacts.

### Generation masks

- Using the exact same mask for generation and final compositing.

- Soft/feathered Generation Masks.

- Elliptical soft masks as a seam solution.

- Expecting `add_original_image=true` alone to remove seams.

### Final compositing

- Always shrinking the Final Mask.

- Always enlarging the Final Mask.

- Using one large rectangle across both eyes.

- Globally replacing every feather width with 8px.

- Using very large 12–24px feather for tiny eye regions.

- Assuming one fixed restoration width works for every character.

### Resolution

- Upscaling the entire portrait without changing the relative face size.

### Request structure

- Generating each eye with separate NovelAI requests unless future evidence shows a benefit.

---

# 29. Current Remaining Risks

The latest pipeline is promising but has not yet been proven universal.

Important unvalidated cases include:

```text
different face sizes
different eye shapes
glasses
hair covering the eyes
non-human faces
unusual anatomy
different portrait resolutions
multiple random seeds
automatic eye detection/masking
user exclusion regions
actual in-game generated portraits
```

A successful result on one or two portraits must not be treated as proof of general reliability.

---

# 30. Recommended Validation Procedure

For future testing:

1. Fix the seed during controlled A/B experiments.

2. Change exactly one variable.

3. Inspect the raw NovelAI output before judging compositing.

4. Verify that the desired facial transformation succeeded.

5. Verify that the Final Mask completely replaces the old feature.

6. Verify that no unnecessary hair, skin, clothing, or accessory pixels are replaced.

7. Verify that pixels outside Final Masks are identical to the source.

8. Test multiple portraits.

9. Test multiple seeds.

10. Test difficult anatomy and occlusion cases.

11. Only then change production defaults.

Do not optimize against one lucky raw generation.

---

# 31. Recommended System Design

The pipeline should conceptually separate:

```text
Expression Definition
        |
        +--> Prompt
        |
        +--> Strength
        |
        +--> Feature Mask Strategy
        |
        +--> Feather Strategy
```

For example:

```text
Sleep
  strength: 0.7
  features:
    - left_eye
    - right_eye
  crop: face
  feather: small

Angry
  strength: 0.6
  features:
    - eyebrows
    - eyes
    - mouth
  feather: adaptive

Hot
  strength: 0.6
  features:
    - face regions needed for sweat/flushing
  feather: adaptive
```

The long-term architecture should therefore support **expression-specific mask strategies**, rather than treating every expression as one generic rectangular inpaint operation.

---

# 32. Current Working Conclusions

1. **Mask scope is the strongest practical identity-preservation mechanism.**

2. **Generation Mask and Final Mask must be separate concepts.**

3. **Context, Generation Mask, and Final Mask should be independently controlled.**

4. **NovelAI can create artifacts inside the raw Generation Mask; compositing cannot repair every bad generation.**

5. **The Final Mask should cover the changed feature completely but exclude unrelated pixels.**

6. **Sleep should use separate left/right eye Final Masks.**

7. **Small eye masks require much smaller feather widths than large face masks.**

8. **A global 8px feather is not appropriate for every expression.**

9. **Face-centered cropping is substantially more useful than simply upscaling the full portrait.**

10. **832–1024 face-crop resolution is currently sufficient.**

11. **Sleep requires approximately strength 0.7; general expressions currently use approximately 0.6.**

12. **Negative prompts cannot reliably preserve exact iris colors.**

13. **Pixels that must remain exact should remain outside the Final Mask.**

14. **One NovelAI request can provide context for multiple Final Masks.**

15. **Hard Generation Masks plus separately feathered Final Masks are currently preferable to soft Generation Masks.**

16. **Future improvement should focus on expression-specific feature masks rather than further generic parameter tuning.**

17. **The current pipeline still requires validation across many characters and seeds before being considered universal.**

---

# 33. Short Agent Summary

```text
NovelAI V4.5 expression inpainting — current approach

Core architecture:
    Context > Generation Mask > Final Mask

Do NOT use one mask for both generation and final compositing.

Baseline:
    steps = 23
    scale = 5
    noise = 0.1
    add_original_image = true

General expressions:
    strength ≈ 0.6

Sleep:
    strength = 0.7
    use face-centered crop
    resize crop to 832–1024
    generate both eyes with sufficient surrounding context
    use separate left/right eye Final Masks
    use ~4–8px inward feather
    preserve everything outside Final Masks exactly

Important:
    - Face crop matters more than simple full-image upscaling.
    - Generation Mask should be wider than Final Mask.
    - Final Mask must fully replace the old feature.
    - Smaller is not automatically better.
    - Raw generation artifacts and compositing artifacts are different problems.
    - Inspect raw_result before tuning compositing.
    - Do not globally change all feather widths to 8px.
    - Do not use soft Generation Masks to hide seams.
    - Do not rely on negative prompts to preserve eye color.
    - If pixels must remain exact, exclude them from Final Mask.
    - Prefer one generation request with multiple narrow Final Masks.
    - Use expression-specific mask strategies.
    - Validate across multiple characters and seeds before changing production defaults.
```
