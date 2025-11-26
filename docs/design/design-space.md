设计素材--候选API风格：
  - Line集合 : Text展现为TextLine的集合，TextLine中不含换行。提供Text和TextLine两级API，从TextLine可以查询其所在的Text。缺点是要分配很多Line对象，有下游逻辑误持有Line而使得整个Text无法释放的误用风险。
  - C Style : 不提供Line级对象，而是把行级操作也展平到Text的方法中，用行号索引Line。易于FFI，低生存期泄露风险，低对象分配。代价是易用性不那么OOP。

设计素材--候选接口：
  - ITextBuffer : 对外提供就地修改语义。
  - ITextReadOnly : 对外提供只读语义，但不保证内容不变，底层可能是可变的Buffer。不提供任何编辑操作。
  - ITextSnapshot : 对外提供快照语义，保证内容不变，底层是某种副本。不提供任何编辑操作。
  - ITextImmutable : 对外提供快照语义，保证内容不变，底层是某种增量优化的副本。提供With系列返回表示编辑结果的Immutable实例的编辑操作，内部池化优化。

设计素材--可用数据结构：
  - Rope : 来自AvalonEdit剥离出的Core，可以进一步吸纳rust xi-editor的设计思想进一步改良。
  - PieceTree : 来自对TS VS Code到C#的移植。
  - DererredZip : 只读的引用底层文本，记录所有后续编辑操作，延迟到最终执行一次Zip Merge算法得到成品字符串/字符序列。实现上多用ReadOnlySpan<char> / ReadOnlyMemory<char>来实现零拷贝。可以设计为仅在栈上分配，最外层的Render函数创建DererredZip实例，渲染完成后一次性合成字符串，然后DererredZip及所有中间划分就都可以释放了。

设计素材--各数据结构最适合实现哪些接口？


设计素材--各接口最适合由哪些数据结构实现？