# JSFW.VS.Extensibility2019.VariableUsingView
변수 사용처 한눈에 보기 (( 💙 내가 가장 애정을 가진 프로그램 중 하나! 💙 ))

목적 : C# 과 VB.NET 소스를 볼 때 변수 사용처를 한눈에 보고 싶을때 사용

 C# : 문자열 파싱
 VB : 로슬린





- 보고싶은 변수를 선택(더블클릭)하고 마우스 우측버튼으로 컨텍스트 메뉴를 띄우면 [변수 사용처 보기] 메뉴가 뜬다
![image](https://user-images.githubusercontent.com/116536524/198198649-72d3bb60-85be-4377-b13d-3cbfa26c365f.png)

- 사용된 위치를 보여준다. 
![image](https://user-images.githubusercontent.com/116536524/198201212-0322677b-8897-4bf1-adb8-7972402855de.png)

- 변수 사용보기 창에서 보고 싶은 라인 더블클릭 시 소스창에서 포커스를 해당 라인으로 변경해준다. 
![image](https://user-images.githubusercontent.com/116536524/198201904-21446c18-6bb2-44f1-b917-611740a5b384.png)

- vs 선택라인 외곽선 색상 설정
![image](https://user-images.githubusercontent.com/116536524/198202283-2c157ce8-7a42-4b00-b262-3fdcb3bf40fc.png)







소스내에서 텍스트박스 ( MIT )<br />
[ICSharpCode.AvalonEdit](https://github.com/icsharpcode/AvalonEdit) <br />

---
- package 폴더가 없어 확인해보니<br />
  Visual Studio로 소스 열릴때 알아서 생긴다. 
  그리고 관리자 모드가 아니면 아래와 같은 오류가 발생한다.

- 소스 열었을때 오류 발생시<br />
```diff
-오류		프로젝트에 "GenerateFileManifest" 대상이 없습니다.	JSFW.VS.Extensibility2019.VariableUsingView	
```
해결방법 :: 관리자 모드로 Visual Studio를 열면된다. 

---
