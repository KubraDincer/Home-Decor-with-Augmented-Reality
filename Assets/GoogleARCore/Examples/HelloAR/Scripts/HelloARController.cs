//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.UI;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        public static GameObject SelectedModel = null; //Seçeceğimiz objeyi buna atayacağız.
        public static bool move = false; //Durağan olduğu sürece bu false.
        //Move tuşuna bastığımız zaman sadece true olur ve taşıma işlevini yapabiliriz.
        /// UI elements
        public GameObject editPanel;  //Düzenlem paneli
        public GameObject SelectPanel;  //Dutonların olduğu panel
        public GameObject scalerslider;//Boyutlandırma butonunun slideri;
        public GameObject rotateslider;//Döndürme butonunun slideri;
        public GameObject ColorSliders;//Renkelndirme butonunun içindeki gameobje Sliderleri;
        public Camera FirstPersonCamera;//AR Kameramız
        string msg = " ";
        string touched = " ";
        public GameObject DetectedPlanePrefab;//ARCore içinde hazır script dosyasından çekilen zemin tespiti
        public GameObject AndyPlanePrefab; //Tam düzlem tespiti yaptığında ekrana tıkladığımızda çıkar.
        public GameObject[] furniture; //3D nesnelerimizi diziye atacağız ve oradan çekeceğiz.
        public GameObject AndyPointPrefab;// Pointlerle tespit aşamasına devam ediyorsa çıkar. 

        public GameObject SearchingForPlaneUI;//Android uygulamasının başlangıcında beklerken çıkacak ekran..
        private const float k_ModelRotation = 180.0f;
        private List<DetectedPlane> m_AllPlanes = new List<DetectedPlane>();
        private bool m_IsQuitting = false;

        public void Update()
        {
            _UpdateApplicationLifecycle();//Genel kontrol fonksiyonu çağırıldı.

            Session.GetTrackables<DetectedPlane>(m_AllPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < m_AllPlanes.Count; i++)
            {
                if (m_AllPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;


                    break;
                }
            }
            //Uygulama açıldıktan sonra zemin algılayana kadar diğer paneller açılmaz.
            //Ekranda basit bir gösterge bulunur. Algılama sonrası kaybolur.
            SearchingForPlaneUI.SetActive(showSearchingUI);
            if (editPanel.activeInHierarchy == false)
            {
                SelectPanel.SetActive(!showSearchingUI);
            }
            // If the player has not touched the screen, we are done with this update.
            if (move == false)
            {//Touch=Dokunma
                Touch touch;
                if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                { //Ekrana temas yoksa işlem yapma..
                    return;
                }
                //Dokunduğumuz yerin bilgisini alır.O konumda başka bir obje var mı kontrol eder.
                //Ona göre ya yeni obje koyar yada objeyi seçer.
                TrackableHit hit;
                TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                    TrackableHitFlags.FeaturePointWithSurfaceNormal;

                RaycastHit hit2;//Raycast gerçeklikteki sanalı algılar.
                Ray raycast = Camera.main.ScreenPointToRay(touch.position);
                if (Physics.Raycast(raycast, out hit2, Mathf.Infinity))
                {


                    if (SelectedModel != null)
                    { //Nesne tasarımında highlighter eklemiştik.Tüm objeleri buradan aratıyoruz.
                        SelectedModel.transform.Find("highlighter").gameObject.SetActive(false);
                    }

                    SelectedModel = hit2.transform.gameObject;
                    SelectedModel.transform.Find("highlighter").gameObject.SetActive(true);
                    EnableEditing();//Obje ekledikten sonra düzenleme paneli açılır.


                }
                else
                {
                    if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit) && (editPanel.activeInHierarchy == false))
                    {
                        // Use hit pose and camera pose to check if hittest is from the
                        // back of the plane, if it is, no need to create the anchor.
                        if ((hit.Trackable is DetectedPlane) &&
                            Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                                hit.Pose.rotation * Vector3.up) < 0)
                        {
                            Debug.Log("Hit at back of the current DetectedPlane");
                        }
                        else
                        {
                            // Choose the Andy model for the Trackable that got hit.
                            GameObject prefab;
                            if (hit.Trackable is FeaturePoint)
                            {//mavi android nesnesi 
                                prefab = AndyPointPrefab;
                            }
                            else
                            {//yeşil androib nesnesi
                                prefab = AndyPlanePrefab;
                            }

                            /*İlk dokunmada yeşil(andy) nesne başlar.
                            Camerayı hareket ettirsek(yönü veya açısı) değişse bile objenin sabit konumda kalması
                            ve kamera görüşünden çıkıp sonra tekrar görüş açısına girse bile obje orda gözükür.
                            Andy modelini anchorun yeni elemanı yapıp kaydettik. */ 
                            var andyObject = Instantiate(prefab, hit.Pose.position, hit.Pose.rotation);
                            andyObject.transform.Rotate(0, k_ModelRotation, 0, Space.Self);
                            var anchor = hit.Trackable.CreateAnchor(hit.Pose);
                            andyObject.transform.parent = anchor.transform;
                        }
                    }
                }
            }
            else
            { //Move= Hareket,taşıma bölümü
                msg = "in moving ";
                Touch touch;
                if (Input.touchCount == 1 && (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                {/*Ekrana dokunduğumuz süreçte koordinatları alıyor.Obje o konuma aktarılıyor. */
                    msg = "ssasd ";
                    TrackableHit hit;
                    TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                   TrackableHitFlags.FeaturePointWithSurfaceNormal;

                    if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
                    {
                        msg = "hot raycast ";
                        if (hit.Trackable is DetectedPlane)
                        {//Son konum aktarılıyor.
                            SelectedModel.transform.position = hit.Pose.position;
                        }

                    }
                }
            }

        }

  
        private void _UpdateApplicationLifecycle()
        {//Genel uygulama ayarları;Kamera,zaman aşımı,bağlantı ayarları...
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            } //Zaman aşımı sonrası uyku moduna geçiş komutları 
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {//Uygulamayı kapatma fonksiyonu.
                return;
            }

            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        private void _DoQuit()
        {
            Application.Quit();
        }


        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }
        void OnGUI()
        {

            GUI.Label(new Rect(10, 10, 1000, 202), "count" + msg);

            GUI.Label(new Rect(10, 20, 1000, 202), "touched" + touched);
        }

        public void enablebir()
        {
            AndyPlanePrefab = furniture[0];
        }
        public void enableiki()
        {
            AndyPlanePrefab = furniture[1];
        }
        public void enableuc()
        {
            AndyPlanePrefab = furniture[2];
        }

        public void enabledort() { 
        AndyPlanePrefab=furniture[3];
        }

        public void enablebes() { 
        AndyPlanePrefab=furniture[4];
        }

        public void enablealti() { 
        AndyPlanePrefab=furniture[5];
        }
   
       public void EnableEditing()
        {
            SelectPanel.SetActive(false);
            editPanel.SetActive(true);
            ColorSliders.SetActive(false);
        }
       /*Select panelinden bir obje oluşturup o objenin üstüne tıkladığımızda 
            düzenleme paneli(EnableEditing()fonksiyonu) açılır.
            quit butonu(QuitEditing() fonksiyonu) ile edit panelinden çıkabiliriz.*/
        public void QuitEditing()
        {
            SelectedModel.transform.Find("highlighter").gameObject.SetActive(false);
            SelectedModel = null;//seçili obje içi boşaltılıyor.
            ColorSliders.SetActive(false);
            SelectPanel.SetActive(true);
            editPanel.SetActive(false);
            move = false;
        }
        public void EnableRotate()
        {  /* Burada sadece y ekseninde 360 derece dönüş yapması istendiği için
        sc vektörüne böyle bir tanım yapıldı.
        Boyutlandırmada objenin büyüme oranı eşit olması gerektiği için 
        x,y,z aynı anda etkilenmişlerdi.
        Ama burada sadece y ekseniyle ilgileniyoruz.
        */
            ColorSliders.SetActive(false);
            rotateslider.SetActive(true);
            scalerslider.SetActive(false);
            move = false;
           
        } 
        public void Rotate(Slider sl)
        {//Döndürme slideri aktif
            Vector3 sc = new Vector3(0, sl.value*360, 0);
            Debug.Log("scA" + sl.value);//Consola bilgi amaçlı yazım.
            SelectedModel.transform.rotation = Quaternion.Euler(sc);
            Debug.Log("scale" + transform.localScale);
            
        }
        public void EnableScaler()
        { //Boyutlandırma Slideri aktif.
            ColorSliders.SetActive(false);
            scalerslider.SetActive(true);
            rotateslider.SetActive(false);
            move = false;
            
        }
        public void Scale(Slider sl)
        { float val = sl.value+1;
        Vector3 sc = new Vector3(val, val, val);// x,y,z koordinatları
        Debug.Log("scA" + sl.value); //Consola bilgi amaçlı yazım.
            SelectedModel.transform.localScale = sc;
            //Seçilen objenin Scale özelliğini(x,y,z değerlerini) slider ile değiştirme
        }

     

        public void DeleteObject()
        {/*Destroy() Unity'de obje silmek için kullanılan hazır fonksiyondur.
         Seçilen obje birden fazla türeyebilir. Burada silme işlemi yapılırken aynı prefabın
         diğer üyeleri etkilememesi gerekir. */
            Destroy(SelectedModel.transform.parent.gameObject);
            SelectedModel = null;
            SelectPanel.SetActive(true);
            editPanel.SetActive(false);
            ColorSliders.SetActive(false);
        }
        public void MoveObject()
        { /*Taşınabilirlik,hareketin tek true olduğu fonksiyon. 
          Diğer fonksiyonlarda move=false olduğu için sabit noktada hareket yapıyorlar.
          Dönme ve büyüme..    */
            move = true;
            SelectPanel.SetActive(false);
            ColorSliders.SetActive(false);
            rotateslider.SetActive(false);
        }


        public void EnableColor()
        { /*Nesne tasarımı yaparken model ve color isimli gameobjectler oluşturup,
        renklendirilecek parçaları color içine almıştık.
        Burada o kısımları Find() yardımıyla önce model ismini sonradan da color ismini çekiyoruz.
        Sliderden renk tonunu ayarlayıp yeni material oluşturcaz.*/
            move = false;
            scalerslider.SetActive(false);
            rotateslider.SetActive(false);
            ColorSliders.SetActive(true);

            Material mat = new Material(Shader.Find("Standard"));
            GameObject mod = SelectedModel.transform.Find("model").gameObject;
            GameObject ss = mod.transform.Find("color").gameObject; 
            foreach (Transform child in ss.transform)//color içindeki parçaları alıyor.
            {
                Renderer rend = child.gameObject.GetComponent<Renderer>();
                rend.material = mat;
                rend.material.SetColor("_Color", new Color(0, 0, 0));
                
            }
        }

        public void ChangeRed(Slider s)
        {
            foreach (Transform child in SelectedModel.transform.Find("model").gameObject.transform.Find("color").gameObject.transform)
            {
                Renderer rend = child.gameObject.GetComponent<Renderer>();
                rend.material.SetColor("_Color", new Color(s.value, rend.material.color[1], rend.material.color[2]));
            } // s.value =>Sliderdeli veri değer, diğerleri de ona eşdeğer renkler..

        }

        public void ChangeGreen(Slider s)//Temel olarak Kırmızı, Yeşil ve Mavi renk tonları alınıyor.
        {
            foreach (Transform child in SelectedModel.transform.Find("model").gameObject.transform.Find("color").gameObject.transform)
            {
                Renderer rend = child.gameObject.GetComponent<Renderer>();
                rend.material.SetColor("_Color", new Color(rend.material.color[0], s.value, rend.material.color[2]));
            }
        }

        public void ChangeBlue(Slider s)
        {
            foreach (Transform child in SelectedModel.transform.Find("model").gameObject.transform.Find("color").gameObject.transform)
            {
                Renderer rend = child.gameObject.GetComponent<Renderer>();
                rend.material.SetColor("_Color", new Color(rend.material.color[0], rend.material.color[1], s.value));
            }
        }

    }
}
