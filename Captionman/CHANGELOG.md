## 1.0.4
- Fix caption UI for R.E.P.O. v0.5.0 the Sunstone update by resolving the HUD canva and game font after scene loads
- Update captionsEN.csv
  - Add (Enemy) Pony's captions
  - Fix `enemy animal attack loop` entity tags: use `EnemyAnimalAnim` for animal attacks. 
    - Note: Pony do use same audio clip when it was crawling on the ceiling.
  - Add (Enemy) Halloween version of Upscream's captions
  - Add (Enemy) Halloween version of Gnome's captions
  - Add (Valuable) Shooting Cowboy's captions
  - Add (Valuable) Banjo's captions
  - Add (Valuable) Mouth Harp's captions
  - Add (Valuable) Harmonica's captions
  - Add (Valuable) Bird Cage's captions
  - Add (Valuable) Cowboy Action Figure's captions
  - Add (Valuable) Golf Cart's captions
  - Add (Valuable) Rattlesnake's captions
  - Add (Valuable) Snake in a Can's captions
  - Add (Valuable) Explosive Tower's captions
    - Note: Explosive Tower dynamite is using igniting sound file from Banger. Use `ValuableDesertExplosiveTowerDynamite` for Explosive Tower dynamite and `EnemyBangAnim` for Banger.
  - Add (Valuable) `Glass Big`'s captions
  - Add (Item) Revolver's captions
  - Add (Prop) Minecart's captions
  - Add (Level) Halloween version of extraction's captions
  - Add (Level) Arctic Door's captions
  - Add Additional captions (rock slide, shack floor breaking, and desert glass breaking)

## 1.0.3
- Reduce default Background Opacity value from `0.7` to `0.4`
- Update both `README.md` files with new configuration information
- Update captionsEN.csv
  - Fix `item laser cannon buildup loop` and `item laser cannon shoot loop` entity tags: use `ItemCartLaser` for C.A.R.T. Laser and `ItemGunLaser` for Photon Blaster
  - Disable `item laser gun shoot` caption: covered by `item laser cannon shoot loop` with `ItemGunLaser` entity tag
  - Fix `item stun gun shoot0X` entity tags: use `ItemGun` for Boltzap and `ItemStunBaton` for Prodzap
  - Disable Phase Bridge's no and off caption
  - Reword Phase Bridge's grow, no battery, and no grow` captions
  - Disable `grenade countdown` caption: Use beeping sound instead
  - Fix `enemy hidden man footstep sprint0X` entity tags: use `EnemyHiddenAnim` for Hidden and `BirthdayBoyAnim` for Birthday Boy
  - Reword Apex Predator and Rubber Duck captions: `enemy duck quack0X` under `EnemyDuckAnim` for Apex Predator and `enemy duck notice0X` under `ItemRubberDuck` for Rubber Duck captions
  - Add TV's power toggle captions
  - Add Elas's bark happy and bark idle captions
  - Add Gambit's 7 music captions
  - Add Spewer's roam captions
  - Add Heart Hugger's scare, gas, and thrust captions
  - Add Bella's glitch sound captions
  - Add Trudge's trailing mace, tell, attack, and vacuum vortex captions
  - Reword Enemy Valuable explode captions
  - Add concrete wall cracking captions
  - Add Birthday Boy's death and breathing captions
- Add `CHANGELOG.md` file for Thunderstore package release notes

## 1.0.2
- Implement Conditional Captioning: Shared sounds now show the correct caption based on their source entity (enemy, item, valuable). Adds an entity column to the caption CSV matched to the owner's C# component type.
- Implement Stop Console Spam: Suppress repeated identical messages within a 5-seconds window when enabled
- Update Icon
- Update captionsEN.csv
  - Add new column: `entity`
  - Fix `enemy duck quack/notice` entity tag: use `EnemyDuckAnim` and Rubber Duck is `ItemRubberDuck` entity.
  - Fix `item vehicle drifting loop` entity tag: use `ItemVehicle`
  - Update/spellcheck/proofread monster captions such as Apex Predator, Banger, Birthday Boy, Clown, Elsa, and more
  - Update player captions by re-enable `player death explosion0X` but it will display `[semibot death]` instead because `player death0X` do not play sound in the game
  - Update defibro captions
  - Update valuable captions such as `Pill Bottle`
  - Update coin UI by enable global
  - Add special trap caption such as `Money Head`
  - Add special door caption from Swiftbroom Academy
  - Disable extra extraction point tube captions
  - Disable valuable/trap item captions such as `Propane Tank` and `Blender`
- Update main README.md with Conditional Captioning in Technical Details

## 1.0.1
- Remove the `legacyNames` variable and all legacy-fallback logic from the `ResolveCsvPath` method.
- Reduce Proximity Radius from 30 to 15 world unit.
- Update README.md
  - Spellcheck/Proofread
  - Fix a typo in the customization section
  - Update the description section
  - Change `GameAudioRepeatCooldownSeconds` default value from 2.0 to 4.0 in the customization section
  - Update the contribution section
- Update captionsEN.csv
  - Add monster captions (Banger, Bowtie, Cleanup Crew, Chef, Headgrab, and more)
  - Add defibro captions
  - Add shop machine and miscellaneous captions
  - Update existing captions (Champagne Bottle, Extraction Tube, and more)
  - Spellcheck/Proofread existing captions

## 1.0.0
- This is initial release.