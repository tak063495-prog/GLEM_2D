# 解析手法・仮定・適用範囲

本書は、GLEMが実際に実装している式を説明します。設計基準そのものではありません。設計や安全性判断に使用する前に、有資格の地盤技術者が入力モデル、臨界すべり面、数値収束、適用性を確認してください。

English: [METHODS.md](METHODS.md)

## 座標系と共通項

- `z` は地表面から下向きを正とする深さです。
- 各スライスは重量 `W`、底面長 `ΔL`、底面傾斜角 `α` を持ちます。
- 有効応力強度は、スライス中点が属する層の `c'` と `φ'` を使用します。
- 載荷範囲内のスライスには `q b` を加算します。
- 層に `ru` がある場合は `u = ru σ'v`、ない場合は地下水位による静水圧を使用します。`ru` が優先です。
- 擬静力係数は、実装上の駆動項 `W(sin α + kh)` と、法線力項の `(1 + kv)` に入ります。

## フェレニウス法（簡便法）

```text
FS = Σ[c' ΔL + N' tan φ'] / Σ[W* (sin α + kh)]
N' = max(0, W* (1 + kv) cos α - u ΔL)
W* = W + q b
```

円弧すべり面に対する全体的なモーメント型釣合いを扱いますが、スライス間力を無視します。一般的な円弧問題では保守的となる傾向がありますが、全ての形状・荷重条件で保証されるものではありません。

## 簡易ビショップ法

```text
mα = cos α [1 + tan α tan φ' / FS]
FS = Σ{[c' b + (W*(1 + kv) - u b) tan φ'] / mα}
     / Σ[W* (sin α + kh)]
```

初期値は `FS = 1.0`、既定の収束許容値は `0.001`、最大反復回数は200回です。結果には収束の成否を表示します。円弧すべり面を対象とし、スライス間せん断力を無視します。

## GLEMのヤンブ近似

互換性のためコード上で `JanbuGeneralized`、ファイル形式で `janbu_generalized` と呼ぶモードは工学的近似であり、スライス間力関数を独立に解く完全なヤンブ一般法の再現ではありません。画面上は「ヤンブ型（GLEM近似）」と表示します。非円形すべり面では、フェレニウス型の抵抗力・駆動力和に次の補正を適用します。

```text
di = max(0, Wi sin αi)
αbar = Σ(di αi) / Σdi
λc = min(2.0, 1 + Σ[di |αi - αbar|] / Σdi)
FS = 抵抗力 / (λc × 駆動力)
```

円弧と判定された場合は `λc = 1.0` です。この角度分散補正は決定論的で回帰試験されていますが、公開されたヤンブ補正の考え方を参考にしたGLEM固有の近似です。完全なヤンブ一般法を実装する商用ソフトと同等とは扱えません。この注意事項は設定画面、結果画面、HTMLレポートに表示します。

## 沈下モデル

GLEMは次の一次元成分を合算します。

- 即時沈下：`Es`、ポアソン比、矩形載荷の弾性影響係数。
- 一次圧密：`Cc`/`Cr`、`e0`、初期有効応力、任意の先行圧密圧力を用いる常用対数式。
- 二次圧縮：一次圧密終了後の `Cs log10(t/tc)`。

時間変化にはTerzaghi型の `Tv = cv t / Hdr²` を使用します。`Hdr` は片面排水では層厚、両面排水では層厚の半分です。鉛直一次元排水と層中点で代表する物性を仮定します。

## 適用範囲と既知の制限

- 二次元解析のみで、三次元端部効果は扱いません。
- 各入力層内を均質とし、有効応力Mohr–Coulomb強度を使用します。
- 不飽和吸引、ひずみ軟化、進行性破壊、補強材、アンカー、確率論的信頼性解析は対象外です。
- `ru`を入力しない場合の地下水は静水圧であり、非定常浸透は対象外です。
- 擬静力地震係数は動的応答解析ではありません。
- 円弧探索はグリッド＋局所細分化であり、数学的な大域最小値を保証しません。
- 沈下は一次元で、側方変形、段階施工、非線形剛性変化、連成圧密は対象外です。
- 参照ケースとの数値一致は、個別プロジェクトの地盤モデル妥当性を保証しません。

## 固定参照ケース

`tests/GLEM.Tests/ReferenceCaseTests.cs` に独立計算値を固定しています。

| 手法 | 参照結果 |
|---|---:|
| フェレニウス法・3スライス | `FS = 1.8111263573` |
| 簡易ビショップ法・同じ3スライス | `FS = 1.8291038996` |
| GLEMヤンブ近似・4スライス | `λc = 1.1759747463`, `FS = 1.4997582286` |

スライス入力と厳しい許容差をテスト内に保持し、式や符号の変更による結果変化を検出します。

## 参考文献

- Bishop, A. W. (1955), “The use of the Slip Circle in the Stability Analysis of Slopes,” *Géotechnique*, 5(1), 7–17. [DOI: 10.1680/geot.1955.5.1.7](https://doi.org/10.1680/geot.1955.5.1.7)
- Fellenius, W. (1936), “Calculation of the Stability of Earth Dams,” Transactions of the 2nd Congress on Large Dams, Vol. 4, 445–463.
- Janbu, N. (1973), “Slope Stability Computations,” in *Embankment-Dam Engineering: Casagrande Volume*, 47–86.
- Terzaghi, K. (1943), *Theoretical Soil Mechanics*, Wiley.

文献は手法の系譜を示すものです。GLEMの挙動は、手法名だけでなく本書に示す実装式で定義されます。

## 2026-10 review: geometry, convergence, and settlement scope

The horizontal slice width is `b = ΔL cos α`; `ΔL` remains the base chord length.
Weight is the sum of layer unit weight times layer area in each vertical slice, integrated
exactly for the piecewise linear base. A circle uses a chord per slice; geometric refinement
reduces this approximation. For depth positive downward, circular `α = atan2(-(x-cx), z-cz)`.
Signed contributions must cancel for a symmetric static arc. The current ground surface is
flat (`z=0`): a symmetric static circular search has zero driving, so GLEM reports GLEM-2004,
rather than inventing a finite FS. A driving surcharge distribution or seismic coefficient
must be explicitly supplied. Circles crossing below the modeled ground or not defining a
single-valued base are invalid. Only finite, converged candidates enter the minimum.

Immediate settlement uses one **homogeneous elastic half-space** solution. Identical Es/ν
in multiple positive-thickness layers yield the same value as one layer. Heterogeneous Es/ν
or partially missing Es values produce GLEM-2005. Omit Es in all layers for consolidation-only
analysis. This is a safe applicability restriction, not a finite-layer elasticity solver:
the half-space formula has no thickness dependence, and the finite-thickness zero limit is
**NOT IMPLEMENTED** (a ground with no positive-thickness layers yields zero). Existing
`ImmediateSettlementMm`/`SecondarySettlementMm` method names return metres; result records
convert to mm. Secondary compression begins separately in each layer at its own t95,
obtained by inverting the existing large-Tv law, independent of requested output duration.
Zero permeability gives t95=infinity and zero secondary compression. For zero Cc or missing
primary-clock properties, the explicit secondary reference is one second; this corner case
requires engineering interpretation and is not a new rheological model.

The corrected three-slice Bishop reference is the positive root in [1,3] of
`Σ[(25 cos α + W tan20)/(cos α + sin α tan20/F)] - Σ(W sin α)F = 0`,
for W=(100,150,120), α=(10°,25°,15°). Independent bisection gives
`F=1.8291038995586373`, residual ≈2.84e-14 kN/m; the engine uses fixed-point iteration.
Pure cohesion independently gives `F=Σ(c ΔL)/Σ(W sin α)` at kh=kv=u=0.
For a base from (0,1) to (2,3), horizontal area=4 m²: γ=18 gives W=72 kN/m;
an upper layer 2 m thick with γ=10 and a lower layer γ=20 gives W=45 kN/m.

Reference for horizontal width/base length distinction: USACE EM 1110-2-1902,
Figures F-17/G-11, [manual](https://www.publications.usace.army.mil/Portals/76/Publications/EngineerManuals/EM_1110-2-1902.pdf).
The existing pseudo-static formula and GLEM-specific Janbu approximation are preserved;
full force-equilibrium seismic theory and sloping-ground geometry are outside this revision.

### 日本語の適用上の注意

水平幅bと底面長ΔLを区別し、層別の断面積から重量を求めます。水平地表で左右対称な
円弧の静的駆動力はゼロであり、有限の安全率を返しません。非収束候補は最小FSの選定から除外します。
即時沈下は均質な弾性半無限地盤に限定します。異なるEs/νを持つ多層の弾性解と、正の有限層厚を
ゼロへ近づけた極限は未実装です。全層でEsを省略した場合は圧密のみを計算します。
同一物性層の分割不変性を試験しています。二次圧縮開始は層別t95を用い、出力期間を延ばしても
過去の同じ時刻の値が変わりません。既存の擬似静的式とGLEM独自Janbu近似は今回維持しています。

## 追加レビュー: Bishopの許容領域と有限性

初期FS=1または途中反復がmα>0の領域外の場合、形状不適合と即判定せず、
Σ A/[cosα (F+tanα tanφ)]−D=0を最大の極より右でbracketして二分法へ切り替える。
bracket拡大と二分法は残りの反復上限を消費し、未収束候補は採用しない。
正の乾燥抵抗Aでは式は極の右で単調減少。bracketできない混合負抵抗の根は未検証として診断。
独立参照: ΔL1、W100、α=(−60°,0°,60°)、c100、φ45、kh=.1では
R(F)=600/(1−3/F²)+200−30F、参照F=26.750864342576584。
cx0/cz−1/r6、γ18の17条体円弧では参照F=33.280056023023604。
Coreは非有限入力・結果を拒否。制御点のXは有限・重複なし、深度は[0,H]。
地盤外の点を切り捨てたり実傾斜角を±89°に変更したりせず、不正な面を診断する。
