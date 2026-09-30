Analyze the single supplied Base portrait for all requested facial expressions. Each `[State: Name]` describes a separate inpaint result. Use the same visible face as the identity reference for every state, but generate expression tags independently.

Return exactly:
{"states":[{"state":"","positive":[],"negative":[]}]}

Include exactly one item per requested state and copy state names exactly. Each item must contain only `state`, `positive`, and `negative`. Tags are short English strings. No Markdown, prose, scores, omitted, merged, or added states.

For each `positive`, describe the requested expression and visible facial identity needed for that result: eye/eyelid state, brows, mouth, nose, face/fur color, and facial markings. Include ears or bangs only if directly affected.

If a state's eyes are open and visible, always include precise eye/iris color and any distinctive pupil color or shape. If the requested state has fully closed eyes, do not include iris color, pupil color, pupil shape, `visible pupils`, or other tags implying visible eyes. Expression requirements take priority over normally preserved eye details.

Describe visible colors precisely when possible (e.g. `pale blue irises`, `dark reddish-brown eyes`, `warm gray fur`). Do not guess unclear details.

For each `negative`, include facial results that contradict that state's expression or alter relevant visible identity features. Keep expression-dependent tags state-specific. For example, Sleep may use `closed eyes` in positive and `open eyes`, `visible irises`, or `visible pupils` in negative; do not copy them into unrelated states.

Exclude clothing, jewelry, body, pose, background, framing, art style, overall hairstyle, and non-face details. Do not infer hidden features, invent details, duplicate concepts, or place the same concept in both lists.

Per state: maximum 12 positive and 10 negative tags. Prefer precise tags over redundant ones. Use `[]` when no justified tags exist.
