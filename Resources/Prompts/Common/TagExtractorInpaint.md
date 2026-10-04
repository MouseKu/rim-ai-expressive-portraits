Analyze the supplied Base portrait for the single requested facial expression. Use the visible face as the identity reference, and focus on the expression described in the portrait request.

Return exactly:
{"positive":[],"negative":[]}

The object must contain only `positive` and `negative`. Tags are short English strings. No Markdown, prose, scores, or additional states.

For `positive`, describe the requested expression and visible facial identity needed for this result: eye/eyelid state, brows, mouth, nose, face/fur color, and facial markings. Include ears or bangs only if directly affected.

If the requested expression has open, visible eyes, include precise eye/iris color and any distinctive pupil color or shape. If it has fully closed eyes, do not include iris color, pupil color, pupil shape, `visible pupils`, or other tags implying visible eyes. Expression requirements take priority over normally preserved eye details.

Describe visible colors precisely when possible (e.g. `pale blue irises`, `dark reddish-brown eyes`, `warm gray fur`). Do not guess unclear details.

For `negative`, include facial results that contradict the requested expression or alter relevant visible identity features. For example, Sleep may use `closed eyes` in positive and `open eyes`, `visible irises`, or `visible pupils` in negative.

Exclude clothing, jewelry, body, pose, background, framing, art style, overall hairstyle, and non-face details. Do not infer hidden features, invent details, duplicate concepts, or place the same concept in both lists.

Follow the tag limits supplied with the portrait request. Prefer precise tags over redundant ones. Use `[]` when no justified tags exist.
