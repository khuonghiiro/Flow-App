import * as THREE from 'three';
import { EnvironmentConfig } from '../../types/scene';

export class SceneLighting {
  public ambientLight: THREE.AmbientLight;
  public sunLight: THREE.DirectionalLight;
  public hemiLight: THREE.HemisphereLight;
  public fog: THREE.FogExp2;
  private scene: THREE.Scene;

  constructor(scene: THREE.Scene) {
    this.scene = scene;

    this.ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    this.scene.add(this.ambientLight);

    this.hemiLight = new THREE.HemisphereLight(0xffeeb1, 0x080820, 0.5);
    this.hemiLight.position.set(0, 50, 0);
    this.scene.add(this.hemiLight);

    this.sunLight = new THREE.DirectionalLight(0xfffaed, 1.8);
    this.sunLight.position.set(15, 25, 15);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.width = 2048;
    this.sunLight.shadow.mapSize.height = 2048;
    this.sunLight.shadow.camera.near = 0.5;
    this.sunLight.shadow.camera.far = 100;
    const d = 20;
    this.sunLight.shadow.camera.left = -d;
    this.sunLight.shadow.camera.right = d;
    this.sunLight.shadow.camera.top = d;
    this.sunLight.shadow.camera.bottom = -d;
    this.sunLight.shadow.bias = -0.0005;
    this.scene.add(this.sunLight);

    this.fog = new THREE.FogExp2(0x1a162b, 0.015);
    this.scene.fog = this.fog;
  }

  public applyEnvironment(env: EnvironmentConfig): void {
    switch (env.sky_time) {
      case 'sunset':
        this.scene.background = new THREE.Color(0x321626);
        this.fog.color.set(0x2d172e);
        this.ambientLight.color.set(0xff9977);
        this.ambientLight.intensity = 0.6;
        this.hemiLight.color.set(0xff7755);
        this.hemiLight.groundColor.set(0x221133);
        this.sunLight.color.set(0xffaa55);
        this.sunLight.intensity = 2.2;
        this.sunLight.position.set(20, 10, 15);
        break;

      case 'sunrise':
        this.scene.background = new THREE.Color(0x232042);
        this.fog.color.set(0x2b2247);
        this.ambientLight.color.set(0xffc599);
        this.ambientLight.intensity = 0.5;
        this.hemiLight.color.set(0xffd5aa);
        this.hemiLight.groundColor.set(0x112233);
        this.sunLight.color.set(0xffeedd);
        this.sunLight.intensity = 1.8;
        this.sunLight.position.set(-20, 12, 10);
        break;

      case 'night':
        this.scene.background = new THREE.Color(0x070913);
        this.fog.color.set(0x090b17);
        this.ambientLight.color.set(0x334466);
        this.ambientLight.intensity = 0.3;
        this.hemiLight.color.set(0x445588);
        this.hemiLight.groundColor.set(0x050711);
        this.sunLight.color.set(0x88aaff);
        this.sunLight.intensity = 0.7;
        this.sunLight.position.set(10, 25, -10);
        break;

      case 'noon':
      default:
        this.scene.background = new THREE.Color(0x88bbee);
        this.fog.color.set(0x99ccee);
        this.ambientLight.color.set(0xffffff);
        this.ambientLight.intensity = 0.5;
        this.hemiLight.color.set(0xddeeff);
        this.hemiLight.groundColor.set(0x334422);
        this.sunLight.color.set(0xffffff);
        this.sunLight.intensity = 2.0;
        this.sunLight.position.set(15, 30, 10);
        break;
    }

    if (env.weather) {
      this.fog.density = env.weather.fog ?? 0.015;
    }
  }
}
