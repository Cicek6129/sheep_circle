# Sheep Circle — Asset Üretim Promptları

Oyunun şu anki görselleri `Assets/Editor/GameSceneBuilder.cs` içinde kodla üretilen
beyaz siluetler. Gerçek art ile değiştirmek için aşağıdaki promptlar kullanılabilir.
Promptlar İngilizce yazıldı (üreteçler İngilizce'de daha tutarlı sonuç veriyor),
açıklamalar Türkçe.

---

## 0. Her prompt'un başına eklenecek ortak stil bloğu

```
top-down orthographic view, camera directly overhead, flat 2D vector game art,
chunky rounded shapes, thick soft outlines, casual mobile game style (Poki / hyper-casual),
bright cheerful farm palette, simple cel shading with one soft highlight,
centered, full body, subject facing RIGHT (east), isolated on a fully transparent
background, PNG with alpha, 512x512 square canvas, small even margin around the subject
```

Ve her prompt'un sonuna negatif blok:

```
--no perspective, side view, three-quarter view, ground, drop shadow, background,
text, watermark, frame, multiple objects, photorealism, 3D render
```

**Neden bu kısıtlar:** `Animal.Apply()` sprite'ı hareket yönüne göre döndürüyor
(`Animal.cs:225`), 0° = sağ. Sprite'lar tepeden bakışta sağa bakmazsa hayvanlar
yan yan yürüyor görünür. Gölge de kod tarafında ayrı bir sprite olmalı, art'a
gömülmemeli — yoksa dönerken gölge de dönüyor.

---

## 1. Hayvanlar (zorunlu — 5 adet)

> **ÖNEMLİ — ilk denemede yaşanan hata:** üreteçler "seen from directly above"
> ifadesini yok sayıp hayvanları **yan profilden** çiziyor. Yan profil bu oyunda
> işe yaramaz: sprite hareket yönüne döndüğü için (`Animal.cs:225`) hayvan
> çemberin tepesinde baş aşağı yürür. Kamera açısını kelimeyle değil,
> **cümleyle tarif etmek** gerekiyor. Aşağıdaki promptlarda büyük harfli
> kısımlar bilerek öyle — yumuşatma.
>
> Ayrıca: **her asset ayrı görsel olarak üretilmeli**, tek sayfada toplu değil.
> Sayfa halinde gelenler kesilmek zorunda kalıyor ve kenarlar tıraşlanıyor.

Aşağıdaki beş prompt **olduğu gibi**, tek tek yapıştırılacak. Parçalama yok,
sayfa halinde toplu üretim yok.

### Koyun (`sheep.png`)
```
Bird's-eye view from a drone hovering directly above a single fluffy sheep
standing on flat ground. The camera looks straight down at 90 degrees: you see
ONLY the animal's back and the top of its head. The body is a rounded oval mound
of cream-white wool made of soft bumpy curls, filling most of the frame. A small
dark grey head pokes out at the front edge with two tiny ears sticking out
sideways, seen from directly above so no face is visible. Only the tips of four
hooves peek out from under the wool at the sides. A tiny tail at the back. This
is a top-down game sprite, drawn the way a car is drawn in a top-down racing
game, except it is a sheep.

Style: flat 2D vector game art, thick dark brown outlines, simple cel shading
with one soft highlight, bright cheerful farm colors, casual mobile game look.

Framing: single object, centered, head pointing to the RIGHT edge of the image,
small even margin, fully transparent background, 512x512 square, absolutely NO
shadow under the animal, nothing else in the image.

Avoid: side view, side profile, visible face, eyes seen from the side, legs
drawn in profile, drop shadow, ground, grass, background color, sprite sheet,
multiple animals, text, watermark.
```

### İnek (`cow.png`)
```
Bird's-eye view from a drone hovering directly above a single holstein dairy cow
standing on flat ground. The camera looks straight down at 90 degrees: you see
ONLY the animal's back and the top of its head. The body is a long white oval
seen from above, clearly longer than a sheep, covered in irregular black
holstein patches. A head pokes out at the front edge with a pinkish-brown muzzle,
two small horns and floppy ears sticking out sideways, seen from directly above
so no face is visible. Four hooves peek out at the sides. A thin tail with a
tuft trails at the back. This is a top-down game sprite, drawn the way a car is
drawn in a top-down racing game, except it is a cow.

Style: flat 2D vector game art, thick dark brown outlines, simple cel shading
with one soft highlight, bright cheerful farm colors, casual mobile game look.

Framing: single object, centered, head pointing to the RIGHT edge of the image,
small even margin, fully transparent background, 512x512 square, absolutely NO
shadow under the animal, nothing else in the image.

Avoid: side view, side profile, visible face, eyes seen from the side, legs
drawn in profile, drop shadow, ground, grass, background color, sprite sheet,
multiple animals, text, watermark.
```

### Keçi (`goat.png`)
```
Bird's-eye view from a drone hovering directly above a single brown farm goat
standing on flat ground. The camera looks straight down at 90 degrees: you see
ONLY the animal's back and the top of its head. The body is a narrow caramel-tan
oval seen from above, slimmer and shorter than a sheep. A dark brown head pokes
out at the front edge with two backswept horns lying along the back and pointed
ears sticking out sideways, seen from directly above so no face is visible. Four
thin legs peek out at the sides. A short stubby tail at the back. This is a
top-down game sprite, drawn the way a car is drawn in a top-down racing game,
except it is a goat.

Style: flat 2D vector game art, thick dark brown outlines, simple cel shading
with one soft highlight, bright cheerful farm colors, casual mobile game look.

Framing: single object, centered, head pointing to the RIGHT edge of the image,
small even margin, fully transparent background, 512x512 square, absolutely NO
shadow under the animal, nothing else in the image.

Avoid: side view, side profile, visible face, eyes seen from the side, legs
drawn in profile, drop shadow, ground, grass, background color, sprite sheet,
multiple animals, text, watermark.
```

### Tavuk (`chicken.png`)
```
Bird's-eye view from a drone hovering directly above a single plump hen standing
on flat ground. The camera looks straight down at 90 degrees: you see ONLY the
bird's back and the top of its head. The body is a small round golden-yellow
oval seen from above, the smallest of the farm animals. A small head pokes out at
the front edge with a red comb on top and a short orange beak pointing forward,
seen from directly above so no face is visible. Two wing shapes are folded flat
against the sides of the back. A fan of tail feathers spreads at the back. Two
thin orange legs peek out underneath. This is a top-down game sprite, drawn the
way a car is drawn in a top-down racing game, except it is a chicken.

Style: flat 2D vector game art, thick dark brown outlines, simple cel shading
with one soft highlight, bright cheerful farm colors, casual mobile game look.

Framing: single object, centered, head pointing to the RIGHT edge of the image,
small even margin, fully transparent background, 512x512 square, absolutely NO
shadow under the animal, nothing else in the image.

Avoid: side view, side profile, visible face, eyes seen from the side, legs
drawn in profile, drop shadow, ground, grass, background color, sprite sheet,
multiple animals, text, watermark.
```

### Çoban (`shepherd.png`)
```
Bird's-eye view from a drone hovering directly above a single friendly shepherd
standing on flat ground. The camera looks straight down at 90 degrees: you see
ONLY the top of his wide straw hat and his shoulders. The straw hat fills most
of the frame, its brim seen as a circle from above with a mustard-yellow band.
The shoulders and arms of a blue jacket are visible around the edge of the brim.
One arm reaches forward holding a wooden crook staff that points toward the
right edge. The toes of brown boots peek out at the front. No face is visible,
because the hat covers it from this angle. Slightly larger than the farm
animals. This is a top-down game sprite, drawn the way a character is drawn in a
top-down game viewed from directly overhead.

Style: flat 2D vector game art, thick dark brown outlines, simple cel shading
with one soft highlight, bright cheerful farm colors, casual mobile game look.

Framing: single object, centered, body oriented so he faces and walks toward the
RIGHT edge of the image, small even margin, fully transparent background,
512x512 square, absolutely NO shadow under him, nothing else in the image.

Avoid: side view, three-quarter view, facing the camera, visible face, drop
shadow, ground, grass, background color, sprite sheet, multiple characters,
text, watermark.
```

### Yine yandan çizerse — kademeli müdahale

1. Prompt'un **en başına** büyük harfle şunu ekle:
   `TOP-DOWN ORTHOGRAPHIC MAP ICON. CAMERA IS DIRECTLY OVERHEAD.`
2. Onaylanan **ağaç görselini referans/stil görseli olarak** ver — o gerçekten
   tepeden çıkmış, üreteç açıyı ondan yakalıyor.
3. Benzetmeyi değiştir: `a sheep-shaped rug lying flat on the floor,
   photographed from the ceiling` — "halı" kelimesi yan profili tamamen kesiyor.
4. Son çare: hayvanı üstten çizilmiş bir arabayla eşleştir —
   `replace the car in a top-down racing game sprite with a sheep, same camera angle`.

Çoban özel: `isShepherd = true`, çemberi 1.55x hızla turlıyor. Diğerlerinden
görsel olarak net ayrılması lazım — şapka rengi (sarı/hardal) iyi bir işaret.

**Opsiyonel:** `sheepdog.png` — çobanın yanında koşan çoban köpeği; ileride
ikinci bir "özel" birim olarak eklenebilir.

---

## 2. Zemin ve yol (zorunlu)

Şu an düz renk: çim `RGB(107,161,92)`, yol `RGB(199,179,140)`, çember yolu
`RGB(184,161,122)`. Doku ile değiştirilecekse **seamless (tileable)** olmalı.

### Çim dokusu (`grass_tile.png`)
```
seamless tileable top-down grass texture, flat 2D vector game art, medium fresh
green, subtle darker grass tufts scattered evenly, a few tiny white and yellow
wildflowers, completely flat lighting with no gradient and no vignette,
edges tile perfectly on all four sides, 512x512
--no shadow, border, text, perspective, center focal point
```

### Toprak yol dokusu (`road_tile.png`)
```
seamless tileable top-down dirt farm road texture, flat 2D vector game art,
warm sandy beige, faint parallel wheel ruts, small scattered pebbles and dry
grass specks, flat lighting, edges tile perfectly on all four sides, 512x512
--no shadow, border, text, perspective
```

### Yol kenarı şeridi (`road_edge.png`)
```
seamless horizontal strip of a wooden farm fence seen from above, flat 2D vector
game art, weathered light brown planks with darker posts at regular intervals,
tiles seamlessly left to right, transparent background, 512x64
```
Yol kenarlarına döşenirse pist hissi çok artar.

---

## 3. Orta ada dekoru (önerilen)

Merkez ada şu an düz yeşil daire (`RingInner = 1.90` yarıçap). Üzerine
serpiştirilecek tepeden dekorlar — hepsi ayrı şeffaf PNG:

```
[ortak stil bloğu, "facing right" kısmı çıkarılarak] + a <ŞEY> seen from
directly above, flat 2D vector farm game art
```

`<ŞEY>` yerine sırayla:
- `round leafy tree canopy with a hint of trunk in the middle` → `tree.png`
- `small round bush` → `bush.png`
- `round golden hay bale with spiral straw pattern` → `haybale.png`
- `wooden water trough filled with blue water` → `trough.png`
- `stone well with a small wooden roof` → `well.png`
- `red barn with a grey roof` → `barn.png` (ada dışına, köşelere)
- `scarecrow with a straw hat, arms out` → `scarecrow.png`
- `muddy puddle with a soft blue reflection` → `puddle.png`

---

## 4. Efektler ve gölge (önerilen — oyun hissini en çok bunlar değiştirir)

### Gölge (`shadow.png`)
```
soft round black blob shadow, radial blur from 35% opacity center to fully
transparent edge, no outline, perfectly centered, transparent PNG, 256x256
```
Her hayvanın altına ayrı bir SpriteRenderer olarak, **dönmeyen** bir child
objede kullanılmalı.

### Toz bulutu (`dust_puff.png`)
```
small cartoon dust puff cloud, flat 2D vector game art, three overlapping soft
white-beige rounded blobs, thick outline, transparent background, 256x256
```
Hayvan çembere girerken ve çıkarken.

### Çarpışma (`crash_burst.png`)
```
cartoon comic impact burst, flat 2D vector game art, spiky orange and yellow
star shape with white center, thick dark outline, transparent background, 512x512
```

### Puan ikonu (`score_star.png`)
```
chunky five-pointed golden star with a thick dark outline and a small white
highlight, flat 2D vector game art, transparent background, 256x256
```
"+1" popup'ında ve skor sayacının yanında.

---

## 5. UI (önerilen)

### Panel çerçevesi (`panel_wood.png`, 9-slice)
```
wooden signboard panel for a farm game UI, flat 2D vector art, warm brown planks
with a darker border frame and small nail heads in the corners, empty center with
no text, straight edges suitable for 9-slice scaling, transparent background, 512x512
--no text, letters, logo
```

### Buton (`button_green.png`, 9-slice)
```
rounded rectangular game button, flat 2D vector art, fresh green face with a
darker green bottom edge for depth, thick dark outline, empty face with no text,
transparent background, 512x256
--no text, letters, icon
```

### İkonlar (`icon_restart.png`, `icon_sound.png`, `icon_pause.png`)
```
simple white <restart arrow / speaker / pause bars> icon, thick rounded strokes,
flat 2D vector game UI icon, centered, transparent background, 256x256
```

### Logo (`logo.png`)
```
game logo for "SHEEP CIRCLE", chunky rounded 3D-looking cartoon letters in cream
white with a thick brown outline and a green drop shadow, a small fluffy sheep
head peeking over the top of the letters, flat 2D vector style, transparent
background, 1024x512
```

### Oyun ikonu / kapak (`icon_512.png`)
```
mobile game icon: a fluffy white sheep seen from above standing in the middle of
a circular dirt roundabout on green grass, flat 2D vector art, bright cheerful
colors, bold and readable at small size, square composition, 512x512
--no text, logo, border
```

---

## 6. Ses (opsiyonel — ElevenLabs SFX / benzeri)

Kısa, loop'suz, mono, 44.1kHz:
- `sheep_baa` → "single short cartoon sheep baa, cute, dry, no reverb"
- `cow_moo` → "single short cartoon cow moo, low and friendly"
- `goat_bleat` → "single short cartoon goat bleat"
- `chicken_cluck` → "single short cartoon chicken cluck"
- `score_pop` → "short bright UI coin pickup pop, cheerful, 0.3 seconds"
- `crash` → "cartoon comedic crash thud with a squeak, 0.6 seconds"
- `whistle` → "short shepherd whistle, two notes"
- `music_loop` → "cheerful looping farm background music, banjo and light
  percussion, laid back, 60 seconds, seamless loop"

---

## 7. Üretim sonrası: Unity'ye alma

1. PNG'leri `Assets/Art/` içine at.
2. Import ayarları (Inspector'da, her sprite için):
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: **Single**, Pivot: **Center**
   - **Pixels Per Unit = görselin piksel genişliği** (512 üretildiyse 512).
     Sebep: mevcut sprite'lar 1 dünya birimi genişliğinde ve `Animal.Setup()`
     ölçeklemeyi `kind.size` ile yapıyor.
   - Alpha Is Transparency: **açık**, Generate Mip Maps: **kapalı**
   - Compression: None (256/512 gibi küçük dosyalarda fark etmez, kalite kazanır)
3. Tileable dokularda Wrap Mode: **Repeat**, diğerlerinde **Clamp**.

## 8. Üretim sonrası: kodda değişecekler

Renkli, kafası çizime dahil art kullanıldığında `GameSceneBuilder.cs:168-172`
satırlarındaki `SetKind` çağrıları güncellenmeli:

- `bodyColor` → `Color.white` (yoksa art tint'lenip rengi bozulur)
- `showHead` → `false` (ayrı kafa dairesi artık gereksiz)
- `patchSprite` → `null` (inek lekeleri artık body sprite'ının içinde)
- `bodySprite` → yeni sprite (`LoadSprite("sheep")` vb., dosya adları
  `Build()` içindeki `LoadSprite` çağrılarında güncellenmeli)

Sonra **Sheep Circle → Rebuild Game Scene**. Sahne kodla üretildiği için elle
sürükleme gerekmiyor.

Not: `ring.png` her rebuild'de kodla yeniden üretiliyor (`Build()` içindeki
`WriteSprite("ring", ...)`). Çember yoluna el yapımı doku konacaksa o satır
kaldırılmalı, yoksa üzerine yazar.
