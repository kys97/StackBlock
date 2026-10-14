Shader "Hidden/HintTest/BaseColor" {
    Properties { _BaseColor("Base Color", Color) = (1,1,1,1) _Color("Unrelated", Color) = (0,0,0,1) }
    SubShader { Pass {} }
}
