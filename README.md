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

これは [Ram.Type-0 氏の Meshia Mesh Simplification](https://github.com/RamType0/Meshia.MeshSimplification) のフォークです。Unity Job System + Burst による高速・非同期処理の本体はそのままに、外観を保つためのオプション（テクスチャの歪み軽減、自己交差の抑制、マテリアル／UV 境界の保持）と、Cascading UI での実際の三角形数表示を追加しています。

新しいオプションは `MeshSimplifierOptions` のフィールドで制御し、すべて無効にすると元のエンジンと同じ挙動になります。詳細は上記の英語／繁体字中国語セクションの表を参照してください（既定で *Preserve Texture Mapping* / *Suppress Self-Intersection* / *Preserve Material Boundaries* が有効、*Preserve UV Seams* は無効）。

### 使い方

#### NDMF統合

`MeshiaMeshSimplifier`（アバター全体には *Meshia Cascading Avatar Mesh Simplifier*）をモデルにアタッチします。エディターで軽量化結果をプレビューできます。

#### C#から呼び出す

```csharp
using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// 非同期API
await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// 同期API
MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);
```

---

## License

MIT License. Copyright (c) 2025 Ram.Type-0 (original author); fork modifications by touma-tw. See [LICENSE.md](LICENSE.md).
