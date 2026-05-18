// src/app/features/landing/landing.component.ts
import {
  Component, OnInit, OnDestroy,
  ElementRef, ViewChild, AfterViewInit
} from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import * as THREE from 'three';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss']
})
export class LandingComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  private renderer!: THREE.WebGLRenderer;
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private animationId!: number;
  private particles!: THREE.Points;
  private floatingCards: THREE.Mesh[] = [];
  private clock = new THREE.Clock();
  private mouse = new THREE.Vector2();

  stats = [
    { value: '10K+', label: 'Active Jobs' },
    { value: '50K+', label: 'Job Seekers' },
    { value: '5K+',  label: 'Companies'   },
    { value: '95%',  label: 'Match Rate'  }
  ];

  features = [
    {
      icon: '🤖',
      title: 'AI-Powered Matching',
      description: 'Our AI scores your CV against job requirements instantly'
    },
    {
      icon: '⚡',
      title: 'Real-Time Notifications',
      description: 'Get notified the moment your application is reviewed'
    },
    {
      icon: '🎯',
      title: 'Smart Job Search',
      description: 'Filter by location, salary, type and category'
    },
    {
      icon: '📊',
      title: 'Application Tracking',
      description: 'Track all your applications in one beautiful dashboard'
    }
  ];

  constructor(private router: Router) {}

  ngAfterViewInit() {
    this.initThreeJS();
    this.createParticles();
    this.createFloatingCards();
    this.animate();
    this.listenToMouse();
    this.listenToResize();
  }

  ngOnDestroy() {
    cancelAnimationFrame(this.animationId);
    this.renderer.dispose();
    window.removeEventListener('mousemove', this.onMouseMove);
    window.removeEventListener('resize', this.onResize);
  }

  goToRegister() { this.router.navigate(['/register']); }
  goToLogin()    { this.router.navigate(['/login']);    }
  goToJobs()     { this.router.navigate(['/jobs']);     }

  // ── Three.js setup ──────────────────────────────────

  private initThreeJS() {
    const canvas = this.canvasRef.nativeElement;
    const w = window.innerWidth;
    const h = window.innerHeight;

    // renderer
    this.renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: true
    });
    this.renderer.setSize(w, h);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);

    // scene
    this.scene = new THREE.Scene();

    // camera
    this.camera = new THREE.PerspectiveCamera(75, w / h, 0.1, 1000);
    this.camera.position.set(0, 0, 5);

    // ambient light
    const ambient = new THREE.AmbientLight(0x00d4ff, 0.3);
    this.scene.add(ambient);

    // point lights
    const light1 = new THREE.PointLight(0x00d4ff, 2, 20);
    light1.position.set(5, 5, 5);
    this.scene.add(light1);

    const light2 = new THREE.PointLight(0x7c3aed, 2, 20);
    light2.position.set(-5, -5, -5);
    this.scene.add(light2);
  }

  private createParticles() {
    const count    = 3000;
    const positions = new Float32Array(count * 3);
    const colors    = new Float32Array(count * 3);

    const color1 = new THREE.Color(0x00d4ff);
    const color2 = new THREE.Color(0x7c3aed);

    for (let i = 0; i < count; i++) {
      // spread particles in a sphere
      positions[i * 3]     = (Math.random() - 0.5) * 30;
      positions[i * 3 + 1] = (Math.random() - 0.5) * 30;
      positions[i * 3 + 2] = (Math.random() - 0.5) * 30;

      // mix colors
      const mix   = Math.random();
      const color = color1.clone().lerp(color2, mix);
      colors[i * 3]     = color.r;
      colors[i * 3 + 1] = color.g;
      colors[i * 3 + 2] = color.b;
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('color',    new THREE.BufferAttribute(colors, 3));

    const material = new THREE.PointsMaterial({
      size:         0.05,
      vertexColors: true,
      transparent:  true,
      opacity:      0.8,
      sizeAttenuation: true
    });

    this.particles = new THREE.Points(geometry, material);
    this.scene.add(this.particles);
  }

  private createFloatingCards() {
    const cardData = [
      { x: -3.5, y:  1.5, z: -2, color: 0x00d4ff },
      { x:  3.5, y: -1.0, z: -3, color: 0x7c3aed },
      { x: -2.0, y: -2.0, z: -4, color: 0x0a7aff },
      { x:  2.5, y:  2.5, z: -5, color: 0x00ffaa },
      { x:  0.0, y:  3.5, z: -6, color: 0xff6b6b },
    ];

    cardData.forEach(data => {
      const geometry = new THREE.BoxGeometry(1.4, 0.8, 0.05);
      const material = new THREE.MeshPhongMaterial({
        color:       data.color,
        transparent: true,
        opacity:     0.15,
        shininess:   100,
        specular:    new THREE.Color(data.color)
      });

      const card = new THREE.Mesh(geometry, material);
      card.position.set(data.x, data.y, data.z);
      card.rotation.set(
        Math.random() * 0.5,
        Math.random() * 0.5,
        Math.random() * 0.3
      );

      // edge glow
      const edges    = new THREE.EdgesGeometry(geometry);
      const lineMat  = new THREE.LineBasicMaterial({
        color:       data.color,
        transparent: true,
        opacity:     0.6
      });
      const wireframe = new THREE.LineSegments(edges, lineMat);
      card.add(wireframe);

      this.scene.add(card);
      this.floatingCards.push(card);
    });
  }

  private animate = () => {
    this.animationId = requestAnimationFrame(this.animate);
    const t = this.clock.getElapsedTime();

    // rotate particles slowly
    this.particles.rotation.y = t * 0.03;
    this.particles.rotation.x = t * 0.01;

    // float cards
    this.floatingCards.forEach((card, i) => {
      card.position.y += Math.sin(t + i * 1.2) * 0.003;
      card.rotation.y  = Math.sin(t * 0.5 + i) * 0.1;
      card.rotation.z  = Math.cos(t * 0.3 + i) * 0.05;
    });

    // camera follows mouse gently
    this.camera.position.x +=
      (this.mouse.x * 0.5 - this.camera.position.x) * 0.05;
    this.camera.position.y +=
      (this.mouse.y * 0.3 - this.camera.position.y) * 0.05;
    this.camera.lookAt(this.scene.position);

    this.renderer.render(this.scene, this.camera);
  }

  private onMouseMove = (e: MouseEvent) => {
    this.mouse.x = (e.clientX / window.innerWidth  - 0.5) * 2;
    this.mouse.y = (e.clientY / window.innerHeight - 0.5) * 2;
  }

  private onResize = () => {
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
  }

  private listenToMouse()  { window.addEventListener('mousemove', this.onMouseMove); }
  private listenToResize() { window.addEventListener('resize',    this.onResize);    }
}