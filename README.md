# Meshia Mesh Simplification (Touma Fork)

A fork of [**Meshia Mesh Simplification** by Ram.Type-0](https://github.com/RamType0/Meshia.MeshSimplification), a Burst-accelerated mesh simplification tool/library for Unity and VRChat.

This fork keeps the original fast, asynchronous Job System + Burst core and adds **appearance-preserving** options aimed at avatar optimization: reducing texture distortion, suppressing self-intersection (clip-through), and keeping material / UV boundaries intact — plus a live "actual resulting triangle count" readout in the cascading avatar UI.

All original credit goes to Ram.Type-0. See the [original repository](https://github.com/RamType0/Meshia.MeshSimplification) and [documentation](https://ramtype0.github.io/Meshia.MeshSimplification/).

- [English](#english)
- [繁體中文](#繁體中文)
- [日本語](#日本語)

---

## English

### What this fork adds

The classic Quadric Error Metrics (QEM) algorithm optimizes for geometric shape only. This fork adds options that also account for **appearance** (UV/material) and for **collapse safety**. Everything is controlled by new fields on `MeshSimplifierOptions`; with all of them off, the behavior is identical to the original engine.

| Option (UI label) | Default | What it does |
| --- | --- | --- |
| **Preserve Texture Mapping** (`UseAttributeAwareError`) | On | Puts UV0 into the error metric itself via a 5D (position + UV) attribute quadric (Garland-Heckbert / Hoppe). The collapse solve returns the optimal merged position **and** UV, so texture mapping is far less likely to stretch. |
| **UV Error Weight** (`UvErrorWeight`) | 1.0 | Geometry-vs-UV trade-off for the above. Higher = protect the texture harder; lower = favor geometric accuracy. |
| **Suppress Self-Intersection** (`ConstrainOptimalPosition`) | On | Clamps the collapsed vertex to the neighborhood of its edge and rejects near-degenerate (sliver) triangles, removing the spikes that poke through other surfaces. |
| **Max Collapse Displacement Factor** (`MaxCollapseDisplacementFactor`) | 2.0 | Strictness of the constraint above (smaller = stricter). |
| **Preserve Material Boundaries** (`PreserveSubMeshBoundaries`) | On | Never merges vertices that belong to different sub meshes (materials), preventing material bleeding across boundaries. |
| **Preserve UV Seams** (`PreserveUVSeams`) | **Off** | Hard-locks vertices that sit on a UV seam. Strong protection, but it is the most reduction-limiting option (it can keep the result well above the requested count), so it is opt-in. Texture mapping is already protected softly by *Preserve Texture Mapping*. |

> Results are **situational**, not universally better. These options let the simplifier handle more cases; pick what fits the mesh and reduction ratio (see *Tuning* below).

#### Restoring the original behavior

Set the six options above to `false` / their legacy values. By default this fork enables *Preserve Texture Mapping*, *Suppress Self-Intersection*, and *Preserve Material Boundaries*; *Preserve UV Seams* is off by default.

#### Tuning guide

- **Geometry-dominant meshes at heavy reduction** (e.g. a shoe from 68k → 6k tris): lower **UV Error Weight** or turn off **Preserve Texture Mapping** for the most aggressive, original-style reduction.
- **Texture-pattern / UV-atlas meshes at moderate reduction** (e.g. striped clothing halved): keep the defaults — this is where attribute-aware error shines and avoids the stripe warping the original produces.
- **Can't reach the target triangle count?** A preserve option is locking vertices. Check **Preserve UV Seams**, **Preserve Surface Curvature**, and **Preserve Material Boundaries**. The cascading avatar UI now shows the real achievable count (see below).

#### Cascading avatar UI: actual triangle count

In the **Meshia Cascading Avatar Mesh Simplifier**, each renderer row's numeric field shows the **actual resulting triangle count** from the NDMF preview (not just the requested target). If a preserve option keeps the result above your target — e.g. you ask for `0` but it can only reach `12345` — the field shows the real value and highlights it. The field stays editable: focus it to type an exact target.

> Requires NDMF preview to be enabled (the same preview that powers the total count).

### Installation

This fork is a drop-in replacement that uses the **same assembly names and namespace** (`Meshia.MeshSimplification`) as the original and declares a `legacyFolders` entry, so it **cannot be installed alongside** the original `com.ramtype0.meshia.mesh-simplification` package — install one or the other.

Add this package to your project via VCC / the Unity Package Manager (e.g. *Add package from git URL* pointing at this repository), then use it exactly like the original.

### How to use

#### NDMF integration

Attach `MeshiaMeshSimplifier` to your models (or use the *Meshia Cascading Avatar Mesh Simplifier* for whole avatars). You can preview the result in EditMode.

#### Use from C#

```csharp
using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// Asynchronous API
await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// Synchronous API
MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);
```

`options` defaults to the appearance-preserving configuration via `MeshSimplifierOptions.Default`.

---

## 繁體中文

這是 [Ram.Type-0 的 Meshia Mesh Simplification](https://github.com/RamType0/Meshia.MeshSimplification) 的 fork。保留原本 Unity Job System + Burst 的高速非同步核心，新增以 **外觀保真** 為目標的選項（主要針對 VRChat 角色）：降低貼圖變形、抑制穿模、保留材質與 UV 邊界，並在 Cascading 介面即時顯示每個物件的實際面數。

### 這個 fork 新增了什麼

原本的 QEM 演算法只最佳化幾何形狀。本 fork 加入同時考量 **外觀（UV／材質）** 與 **收縮安全性** 的選項，全部由 `MeshSimplifierOptions` 的新欄位控制；全部關閉時等同原始引擎行為。

| 選項（UI 名稱） | 預設 | 作用 |
| --- | --- | --- |
| **Preserve Texture Mapping**（`UseAttributeAwareError`） | 開 | 把 UV0 納入誤差度量本身（位置+UV 的 5D 屬性二次型）。解收縮時同時得到最佳合併位置與 UV，大幅降低貼圖被拉扯。 |
| **UV Error Weight**（`UvErrorWeight`） | 1.0 | 上者的「幾何 vs UV」權衡。越大越保貼圖，越小越保幾何。 |
| **Suppress Self-Intersection**（`ConstrainOptimalPosition`） | 開 | 把合併點夾限在原邊附近並拒絕退化（sliver）三角面，消除戳穿其他表面的尖刺。 |
| **Max Collapse Displacement Factor**（`MaxCollapseDisplacementFactor`） | 2.0 | 上者的嚴格度（越小越嚴）。 |
| **Preserve Material Boundaries**（`PreserveSubMeshBoundaries`） | 開 | 禁止跨 submesh（材質）合併，避免材質邊界滲色。 |
| **Preserve UV Seams**（`PreserveUVSeams`） | **關** | 硬鎖 UV 接縫頂點。保護力強，但最會卡住減面（可能讓結果遠高於目標），故預設關閉；貼圖已由 Preserve Texture Mapping 軟性保護。 |

> 效果**視情況而定**，並非一律更好。這些選項讓你能應付更多狀況；依網格與減面幅度挑選（見下方調參）。

#### 還原原始行為

把上述六個選項設為 `false`／原始值即可。本 fork 預設開啟 Preserve Texture Mapping、Suppress Self-Intersection、Preserve Material Boundaries；Preserve UV Seams 預設關閉。

#### 調參指南

- **幾何主導、重度減面**（如鞋子 68k → 6k）：調低 **UV Error Weight** 或關閉 **Preserve Texture Mapping**，取得最接近原版的積極減面。
- **貼圖花紋／UV atlas、中度減面**（如條紋衣物減半）：維持預設——這是屬性感知誤差的甜蜜點，能避免原版會出現的條紋扭曲。
- **降不到目標面數？** 代表某個保留選項鎖住了頂點。檢查 **Preserve UV Seams**、**Preserve Surface Curvature**、**Preserve Material Boundaries**。Cascading 介面現在會顯示實際可達面數（見下）。

#### Cascading 介面：實際面數

在 **Meshia Cascading Avatar Mesh Simplifier** 的清單中，每個 renderer 那一列的數字欄位會顯示 NDMF preview 的**實際簡化面數**（而非只是你要求的目標）。若保留選項讓結果降不到目標——例如你設 `0` 但只能到 `12345`——欄位會顯示實際值並標色提醒。欄位仍可編輯：點進去即可直接輸入目標。需開啟 NDMF preview。

### 安裝

本 fork 使用與原版**相同的組件名與 namespace**（`Meshia.MeshSimplification`）並宣告了 `legacyFolders`，因此**無法與原版 `com.ramtype0.meshia.mesh-simplification` 同時安裝**——擇一即可。透過 VCC／Unity Package Manager（例如 *Add package from git URL* 指向本 repo）加入後，用法與原版相同。

---

## 日本語

これは [Ram.Type-0 氏の Meshia Mesh Simplification](https://github.com/RamType0/Meshia.MeshSimplification) のフォークです。Unity Job System + Burst による高速・非同期処理の本体はそのままに、**見た目を保つ**ためのオプション（主に VRChat アバター向け）を追加しています。テクスチャの歪みの軽減、自己交差（メッシュの突き抜け）の抑制、マテリアル／UV 境界の保持に加え、Cascading UI に「実際の三角形数」表示を追加しました。

オリジナルの功績はすべて Ram.Type-0 氏に帰属します。[オリジナルのリポジトリ](https://github.com/RamType0/Meshia.MeshSimplification)・[ドキュメント](https://ramtype0.github.io/Meshia.MeshSimplification/)もご覧ください。

### このフォークで追加された機能

従来の Quadric Error Metrics (QEM) アルゴリズムは「形状」だけを最適化します。本フォークは、**見た目（UV／マテリアル）** と **マージの安全性** も考慮するオプションを追加します。すべて `MeshSimplifierOptions` の新しいフィールドで制御され、**すべて無効にすると元のエンジンと完全に同じ挙動**になります。

| オプション（UI ラベル） | 既定値 | 機能 |
| --- | --- | --- |
| **Preserve Texture Mapping**（`UseAttributeAwareError`） | オン | UV0 を誤差計算そのものに組み込みます（位置 + UV の5次元属性二次誤差、Garland-Heckbert / Hoppe）。マージ位置の求解が最適な位置**と** UV を同時に返すため、テクスチャの歪みが大幅に起きにくくなります。 |
| **UV Error Weight**（`UvErrorWeight`） | 1.0 | 上記の「形状 vs UV」のバランス。大きいほどテクスチャを強く保持し、小さいほど幾何精度を優先します。 |
| **Suppress Self-Intersection**（`ConstrainOptimalPosition`） | オン | マージ後の頂点を元の辺の近傍に制限し、ほぼ退化した（細長い）三角形を却下します。他の面を突き抜けるスパイク（自己交差）を抑えます。 |
| **Max Collapse Displacement Factor**（`MaxCollapseDisplacementFactor`） | 2.0 | 上記の制限の厳しさ（小さいほど厳しい）。 |
| **Preserve Material Boundaries**（`PreserveSubMeshBoundaries`） | オン | 異なるサブメッシュ（マテリアル）に属する頂点同士をマージしません。マテリアル境界のにじみを防ぎます。 |
| **Preserve UV Seams**（`PreserveUVSeams`） | **オフ** | UV シーム上の頂点を固定します。保護力は強いものの、最も削減を妨げるオプション（結果が目標値より大幅に多く残ることがある）のため既定はオフです。テクスチャ自体は *Preserve Texture Mapping* で緩やかに保護されます。 |

> 効果は**状況によって異なり**、常に良くなるわけではありません。これらのオプションはより多くのケースに対応するためのものです。メッシュと削減率に合わせて選んでください（下記の「チューニング」参照）。

#### 元の挙動に戻すには

上記6つのオプションを `false`／従来値に設定してください。本フォークの既定では *Preserve Texture Mapping*・*Suppress Self-Intersection*・*Preserve Material Boundaries* が有効で、*Preserve UV Seams* は無効です。

#### チューニングガイド

- **形状が支配的なメッシュを強く削減する場合**（例：靴を 68k → 6k 三角形）：**UV Error Weight** を下げるか **Preserve Texture Mapping** をオフにすると、従来どおりの積極的な削減になります。
- **テクスチャ模様 / UV アトラスのメッシュを中程度に削減する場合**（例：ストライプの服を半分に）：既定のままにしてください。属性考慮誤差が活き、従来版で起こりがちなストライプの歪みを防ぎます。
- **目標の三角形数まで下がらない場合**：いずれかの保持オプションが頂点をロックしています。**Preserve UV Seams**・**Preserve Surface Curvature**・**Preserve Material Boundaries** を確認してください。Cascading UI に実際に到達可能な数が表示されます（下記）。

#### Cascading UI：実際の三角形数

**Meshia Cascading Avatar Mesh Simplifier** のリストでは、各 renderer の行の数値フィールドが、要求した目標値ではなく **NDMF プレビューによる実際の三角形数** を表示します。保持オプションにより目標まで下がらない場合——たとえば `0` を指定しても `12345` までしか下がらない場合——実際の値を色付きで表示します。フィールドは編集可能で、フォーカスすれば正確な目標値を入力できます。

> NDMF プレビューを有効にする必要があります（合計数を表示しているものと同じプレビューです）。

### インストール

本フォークは、オリジナルと**同じアセンブリ名・名前空間**（`Meshia.MeshSimplification`）を使用し `legacyFolders` を宣言しているため、オリジナルの `com.ramtype0.meshia.mesh-simplification` パッケージと**同時にはインストールできません**（どちらか一方を使用してください）。

VCC／Unity Package Manager（例：*Add package from git URL* でこのリポジトリを指定）でプロジェクトに追加すれば、使い方はオリジナルと同じです。

### 使い方

#### NDMF統合

`MeshiaMeshSimplifier`（アバター全体には *Meshia Cascading Avatar Mesh Simplifier*）をモデルにアタッチします。エディターで軽量化結果をプレビューしながらパラメーターを調整できます。

#### C#から呼び出す

```csharp
using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// 非同期API
await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// 同期API
MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);
```

`options` は `MeshSimplifierOptions.Default` で、見た目を保持する既定構成になります。

---

## License

MIT License. Copyright (c) 2025 Ram.Type-0 (original author); fork modifications by touma-tw. See [LICENSE.md](LICENSE.md).
