Analyze the supplied character image and Base portrait request. Extract compact image-generation tags describing the character's visible identity.

Return exactly: {"positive":[],"negative":[]}

Use only short English image-generation tags. No Markdown, prose, scores, or additional fields.

For positive, extract identity-defining visual traits: species features, eye/iris color, hair color and hairstyle, bangs, skin/fur color, facial markings, clothing, accessories, distinctive details, and the explicitly requested expression.

Treat the supplied Features: block as authoritative for visual facts such as gender, biological age, race, xenotype, body type, colors, hair, and apparel. If the image is ambiguous or conflicts with supplied data, prefer the supplied data. Use the image for details not supplied. Health conditions are context only; tag visible effects only when supported by the image.

Never output proper nouns, game/mod-specific names, internal labels, race names, xenotype names, or apparel/item names. Convert them into concise generic visual descriptions using their supplied attributes and visible appearance. For example, describe a named race by its visible anatomy and a named apparel item by its visible type, shape, material, and color.

Convert biological age to a broad visual age tag such as `young adult`, never the number. Do not infer age or gender if absent. Convert hex/RGB values to precise visual color tags rather than raw codes. Preserve useful distinctions such as light/dark, warm/cool, muted/vivid, and mixed hues.

Preserve distinctive species features, hairstyle, bangs, eyes, clothing, patterns, trims, markings, and accessories, including their visible shape and color when reliable.

For negative, include unwanted identity or expression changes: missing/extra species features, wrong colors, missing distinctive details, wrong eye state, extra anatomy, text, or artifacts. Do not duplicate positive concepts.

Do not infer hidden or unclear details or invent features. Exclude nonvisual health information, traits, backstory, personality, setting, and unseen body/clothing details.

Ignore the source sprite's presentation. Do not tag view/camera angle, gaze direction, orientation, pose, composition, framing, background, or art style unless explicitly required by the Base portrait request.

Follow the tag limits supplied with the portrait request. Prefer specific visual identity traits over generic details. Remove duplicates and empty tags; use [] when none are justified.
