# Sheep Circle

Poki'deki [Car Circle](https://poki.com/en/g/car-circle) oyununun çiftlik hayvanlı versiyonu.
Ortada dairesel bir ağıl yolu var; hayvanlar üzerinde sabit hızla dönüyor. Dört giriş
yolunda kuyruklar birikiyor ve sıradakini **doğru anda** çembere salmak sana kalmış.

## Açmak

Unity **6000.3.6f1** gerekiyor (`Sheep_Circle/ProjectSettings/ProjectVersion.txt`).

1. Depoyu klonla.
2. Unity Hub → **Add** → depo içindeki **`Sheep_Circle`** klasörünü seç (deponun kökünü değil).
3. İlk açılış paketleri indirip import ettiği için birkaç dakika sürebilir.
4. `Assets/Scenes/Game.unity` sahnesini aç ve Play'e bas.

Sahne zaten Build Settings'te ekli, yani doğrudan build de alınabilir.

## Nasıl oynanır

| Girdi | Etki |
|---|---|
| Yola tıkla / dokun | O yolun sıradaki hayvanını çembere salar |
| `1` `2` `3` `4` | Sırasıyla üst, sağ, alt, sol yol |
| Tıkla veya `R` / `Space` | Oyun bittiğinde yeniden başlat |

Boş bir yere tıklarsan en yakın yol seçilir — mobilde affedici olsun diye.

**Amaç:** hayvanları çembere sokup karşı taraftan çıkarmak. Ağıla giren her hayvan +1 puan.

**Kaybetme:**
- İki hayvan toslarsa. Çarpışma her zaman *çembere girmekte olan* bir hayvanı içerir —
  çemberde dönenlerin hepsi aynı açısal hızda gittiği için birbirlerine yetişemezler.
- Dört yolun kuyruğu da dolarsa (**yol tıkandı**). Yani sürekli hayvan salman gerekiyor.

Puan arttıkça çember hızlanır ve hayvanlar daha sık gelir.

## Hayvanlar

Hepsi çemberde aynı hızda döner; fark **boyut** ve **yola çıkma hızında**.

| | Boyut | Girişi | Not |
|---|---|---|---|
| Koyun | orta | normal | en sık gelen |
| İnek | büyük | yavaş | çok geniş boşluk ister |
| Keçi | küçük | hızlı | dar boşluklara sığar |
| Tavuk | çok küçük | çok hızlı | neredeyse fırlar |
| **Çoban** | büyük | normal | özel birim — aşağı bak |

**Çoban**, orijinaldeki polis arabasının karşılığı. Nadiren ve ancak 5 puandan sonra gelir,
aynı anda birden fazla çıkmaz. Saldığında çemberi trafikten **daha hızlı** turlar, önüne
kattığı her hayvanı peşine takar ve sürüyü birlikte dışarı çıkarır — her biri +1 puan.
Çoban çarpışmaz, arkasındaki sürü de çarpışmaz. Yani çember kalabalıkken saklamaya değer.

## Proje yapısı

```
Sheep_Circle/Assets/
  Scripts/
    RingGeometry.cs   Kavşak geometrisi: çember, giriş/çıkış noktaları, kuyruk yerleri
    Animal.cs         Tek hayvanın durum makinesi (kuyruk → giriş → çember → çıkış)
    AnimalKind.cs     Tür tanımı: görsel, boyut, hız, gelme olasılığı
    EntryLane.cs      Bir giriş yolu + kuyruğu + tıklama collider'ı
    GameManager.cs    Spawn, salma, zorluk artışı, çarpışma ve tıkanma kuralları
    HUD.cs            Skor ve oyun sonu ekranı
  Editor/
    GameSceneBuilder.cs   Sahneyi, prefab'ı ve sprite'ları koddan yeniden üretir
  Art/                Prosedürel üretilmiş PNG'ler (elle çizilmiş asset yok)
```

### Sahneyi yeniden üretmek

Tahtanın ölçüleri `GameSceneBuilder.cs`'in başındaki sabitlerde duruyor (çember yarıçapı,
şerit ayrımı, kuyruk aralığı, kamera boyu). Değiştirdikten sonra Unity'de
**Sheep Circle → Rebuild Game Scene** menüsüne bas; sahne, prefab ve halka sprite'ı
sıfırdan üretilir. Sahneyi elle düzenlemek yerine bu yolu kullanmak daha kolay.

## Notlar

- Girdi için yeni **Input System** kullanılıyor (`Pointer` + `Keyboard`), eski `Input` API'si değil.
- Arayüz yazıları bilerek ASCII: varsayılan TextMesh Pro atlasında `ş ğ ı İ` glifleri yok.
  Türkçe karakter istersen font asset'ini genişletmek gerekiyor.
- Görseller kodda signed-distance function'larla çizilip PNG'ye yazılıyor, o yüzden depoda
  kaynak sanat dosyası yok.
