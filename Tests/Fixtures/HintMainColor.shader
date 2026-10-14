Shader "Hidden/HintTest/MainColor" {
    Properties { [MainColor] _Tint("Tint", Color) = (1,1,1,1) _BaseColor("Unrelated", Color) = (0,0,0,1) }
    SubShader { Pass {} }
}
