# 公式图片索引：01-脉冲神经网络

来源文章：[`01-当你的大脑只有0和1-脉冲神经网络.md`](../01-当你的大脑只有0和1-脉冲神经网络.md)，共 5 条块级公式。
所有 PNG 均为透明底、2x 高清输出，可直接内嵌到网页中。

| 图片 | 对应章节 | LaTeX 源码 |
| --- | --- | --- |
| `formula-01.png` | 第二章：传统人工神经网络的神经元 | `y = f\!\left(\sum_{i} w_i x_i + b\right)` |
| `formula-02.png` | 3.1 节：LIF 模型（连续形式） | `\tau \frac{du}{dt} = -(u - u_{\text{rest}}) + R \, I(t)` |
| `formula-03.png` | 3.1 节：LIF 模型（离散化更新公式） | `u[t] = \beta \, u[t-1] + W \, s[t-1] + I_{\text{ext}}[t]` |
| `formula-04.png` | 3.1 节：发放判定与重置 | `s[t] = \begin{cases} 1, & u[t] \geq \theta \\ 0, & u[t] < \theta \end{cases} \qquad\text{发放后重置：} u[t] \leftarrow 0` |
| `formula-05.png` | 4.1 节：发放率（Rate Coding）的定义 | `r = \frac{\text{脉冲总数}}{\text{神经元数} \times \text{时间步数}}` |
