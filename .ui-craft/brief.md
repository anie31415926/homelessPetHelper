# Product Purpose
A portrait pet-care game where players adopt, feed, wash, groom and heal a cat and a dog, watching each grow into adulthood.

# Primary User
Chinese casual-game players enjoying short, tactile sessions on a portrait phone, with a complete desktop preview.

# Principles
1. The pet, not the controls, is the main character. State changes must be visible on its body and expression.
2. Care feels kind, never punitive. No death, forced waiting, paywall or irreversible failure.
3. A small action deserves a tangible response: disappearing dirt, bubbles, a neat coat or a happy bounce.
4. Desktop is a wider pet diary; mobile preserves the same playable portrait scene.

# Success Metric
A player can wash a visibly dirty pet, feed it and complete a medical visit in the first few minutes, then advance a day and see growth.

# Out Of Scope
- Accounts, real payments and online multiplayer.
- Real-time illness or offline stat decay.
- Production economy balancing and professional veterinary advice.

# Learned Constraints
- 2026-11-16: User approved a combined short outing: randomly select environment puzzle OR cleanup, reach the animal, then turn-based trust negotiation, then return to station. Keep it light enough for a few minutes. Variation should reuse resources, and negotiation should respond to the animal's current condition rather than behave like a hidden capture roll.
- 2026-11-16: The maze/net prototype felt too simple. Do not equate a WeChat mini game with thin gameplay. Build three genuinely distinct rescue experiments (environment puzzle, continuous cleanup, animal guidance) for hands-on comparison before choosing the main mechanic.
- 2026-11-13: User chose illustrated storybook style, mint and coral, rounded Chinese type and lightweight motion.
- 2026-11-13: Add a playable 3D toy-like version using the same care loop and save. Keep the illustrated version available for comparison and WebGL fallback.
- 2026-11-16: New direction is a 2D animal rescue station. Prioritize outdoor rescue and warm animal interaction; simplify supporting care and management. No animal quality tiers, defer player visits. Full loop: station -> outing -> rescue -> station -> care/interaction -> outing or animal outcome.
- 2026-11-16: Build rescue as a separate prototype at /rescue.html, preserving the existing care prototype and its save. First test maze traversal followed by positioning, optional food and timed net casting. Keep mint/coral storybook art from the established direction.
