# Galatea

Treball de Fi de Grau (TFG) — Disseny i Desenvolupament de Videojocs, Universitat de Girona, 2026.

Galatea és un prototip de cuina en primera persona en el qual la jugadora prepara plats per a una criatura alienígena que respon amb un de set estats emocionals. No hi ha puntuació ni retroalimentació explícita — el canvi de color de la criatura i el so són l'únic senyal. La pregunta de disseny explorada és si un sistema de sabors purament emergent pot sostenir l'experimentació guiada per la curiositat sense instruccions explícites.

---

## Contingut del repositori

```
Assets/Scripts/
  Data/           Capa de dades C# pura — FlavorProfile, FlavorCalculator, IngredientInstance, DishResult, FlavorAnalysis
  Systems/        MonoBehaviours d'escena — CookingStation, StationSlot, PlatingStation, DishVessel, SoundManager, IngredientBasket
  Player/         PlayerController, InteractionSystem
  Creature/       CreatureEmotionController
  UI/             NAVIController

Assets/ScriptableObjects/
  Ingredients/    Assets IngredientData per a AlienRoot, CrimsonClove, SpottedBerry
  Processes/      Definicions de variants de procés ContinuousVariant i DiscreteVariant
  Emotions/       Assets EmotionalResponse per als set estats emocionals

CLAUDE.md         Document de referència tècnica emprat com a context persistent de sessió d'IA durant el desenvolupament
```

---

## Com funciona el sistema de sabors

Cada ingredient té un perfil base de sis atributs (dolç, àcid, agre, salat, picant, neutralitzador) en escala [0, 10]. La cocció i el tall modifiquen aquests valors al llarg del temps. Quan s'entrega un plat, el perfil combinat és analitzat per `FlavorCalculator`, que calcula:

- **Sabor(s) dominant(s):** el top 1–2 dels cinc eixos tastables (neutralitzador exclòs). Si tres o més eixos cauen dins la tolerància de codominància, la llista de dominants és buida.
- **balanceScore:** `1 / (1 + variància)` sobre els cinc eixos tastables — una mesura estadística d'uniformitat.
- **Intensitat:** suma dels cinc eixos tastables menys el neutralitzador.

Set regles emocionals s'avaluen en ordre de prioritat: Disgusted → Disappointed → Delighted → Cozy → Refreshed → Spicy → Confused. Vegeu `CLAUDE.md` per a la taula completa de regles i valors de llindar.

---

## Detalls tècnics

- Unity 6 (6000.3.6f1), Universal Render Pipeline
- Unity New Input System
- TextMesh Pro
- Cap dependència de gameplay de tercers

---

## Executar el projecte

Obriu-lo amb Unity 6. L'escena principal es troba a `Assets/Scenes/`. No cal cap configuració addicional — totes les referències a ScriptableObjects estan pre-assignades a l'escena.

Controls per defecte: WASD per moure's, ratolí per orientar la càmera, E per interactuar, R per sol·licitar un ingredient de la cistella, clic esquerre per acariciar la criatura.

---

## Declaració d'ús d'IA

Claude (Anthropic) va ser emprat com a assistent de programació i redacció a partir del març del 2026. `CLAUDE.md` va actuar com a context persistent de sessió — un document tècnic viu actualitzat després de cada sessió de treball per reflectir decisions d'arquitectura, canvis de calibratge i justificacions d'implementació. Totes les decisions de disseny, l'arquitectura i el codi final van ser compresos, revisats i validats per l'autora. La declaració completa es troba a la secció 3.2 de la memòria.
