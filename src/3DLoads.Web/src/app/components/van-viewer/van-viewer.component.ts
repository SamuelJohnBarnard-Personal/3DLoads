import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { ScenarioDto, SolutionResponse } from '../../models/api.models';

interface OverlayLabel {
  id: string;
  text: string;
  color: string;
  x: number;
  y: number;
  visible: boolean;
}

interface HoverCard {
  title: string;
  subtitle: string;
  detail: string;
  x: number;
  y: number;
  color: string;
}

interface PlacementVisualMeta {
  id: string;
  reference: string;
  stopIndex: number;
  position: string;
  size: string;
  color: string;
}

@Component({
  selector: 'app-van-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './van-viewer.component.html',
  styleUrl: './van-viewer.component.scss',
})
export class VanViewerComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('canvas', { static: true })
  private readonly canvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() scenario: ScenarioDto | null = null;
  @Input() solution: SolutionResponse | null = null;

  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private renderer!: THREE.WebGLRenderer;
  private controls!: OrbitControls;
  private animationFrameId = 0;
  private readonly geometryGroup = new THREE.Group();
  private readonly resizeHandler = () => this.resizeRenderer();
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly labelAnchors = new Map<
    string,
    { object: THREE.Object3D; text: string; color: string }
  >();
  private placementMeshes: THREE.Mesh[] = [];

  protected labels: OverlayLabel[] = [];
  protected hoverCard: HoverCard | null = null;

  ngAfterViewInit(): void {
    this.initializeScene();
    this.renderLayout();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['scenario'] || changes['solution']) && this.renderer) {
      this.renderLayout();
    }
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.animationFrameId);
    window.removeEventListener('resize', this.resizeHandler);
    this.controls.dispose();
    this.renderer.dispose();
    this.disposeGroup();
  }

  private initializeScene(): void {
    const canvas = this.canvasRef.nativeElement;

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color('#f3f1ea');

    this.camera = new THREE.PerspectiveCamera(50, 1, 0.1, 2000);
    this.camera.position.set(14, -22, 14);

    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));

    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.enableDamping = true;

    this.scene.add(new THREE.AmbientLight('#fff7ea', 0.7));
    const keyLight = new THREE.DirectionalLight('#ffffff', 0.9);
    keyLight.position.set(18, -12, 24);
    this.scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight('#e8f4ff', 0.5);
    fillLight.position.set(-14, 16, 8);
    this.scene.add(fillLight);

    this.scene.add(this.geometryGroup);
    this.scene.add(new THREE.AxesHelper(4));

    const floor = new THREE.Mesh(
      new THREE.PlaneGeometry(120, 120),
      new THREE.MeshStandardMaterial({ color: '#e7e2d6', roughness: 0.95, metalness: 0 }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.z = -0.01;
    this.scene.add(floor);

    window.addEventListener('resize', this.resizeHandler);
    this.resizeRenderer();
    this.animate();
  }

  private animate(): void {
    this.animationFrameId = requestAnimationFrame(() => this.animate());
    this.controls.update();
    this.updateLabelOverlays();
    this.renderer.render(this.scene, this.camera);
  }

  private resizeRenderer(): void {
    const canvas = this.canvasRef.nativeElement;
    const width = canvas.clientWidth || 800;
    const height = canvas.clientHeight || 500;

    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
  }

  private renderLayout(): void {
    this.disposeGroup();
    this.labels = [];
    this.hoverCard = null;

    if (!this.scenario?.van) {
      return;
    }

    const van = this.scenario.van;
    const vanGeometry = new THREE.BoxGeometry(van.length, van.width, van.height);
    const vanEdges = new THREE.EdgesGeometry(vanGeometry);
    const vanWire = new THREE.LineSegments(
      vanEdges,
      new THREE.LineBasicMaterial({ color: '#0f766e', linewidth: 2 }),
    );
    vanWire.position.set(van.length / 2, van.width / 2, van.height / 2);
    this.geometryGroup.add(vanWire);

    const vanShell = new THREE.Mesh(
      vanGeometry,
      new THREE.MeshStandardMaterial({
        color: '#2a9d8f',
        opacity: 0.08,
        transparent: true,
        side: THREE.DoubleSide,
      }),
    );
    vanShell.position.copy(vanWire.position);
    this.geometryGroup.add(vanShell);

    const frontArrowLength = Math.max(2, van.length * 0.28);
    const frontArrowOrigin = new THREE.Vector3(-0.35, van.width / 2, van.height / 2);
    const frontArrow = new THREE.ArrowHelper(
      new THREE.Vector3(-1, 0, 0),
      frontArrowOrigin,
      frontArrowLength,
      0xbc6c25,
      0.5,
      0.3,
    );
    this.geometryGroup.add(frontArrow);

    const frontLabelAnchor = new THREE.Object3D();
    frontLabelAnchor.position.set(
      frontArrowOrigin.x - frontArrowLength - 0.35,
      frontArrowOrigin.y,
      frontArrowOrigin.z,
    );
    this.geometryGroup.add(frontLabelAnchor);
    this.labelAnchors.set('orientation-front', {
      object: frontLabelAnchor,
      text: 'back',
      color: '#bc6c25',
    });

    const upArrowLength = Math.max(2, van.height * 0.34);
    const upArrowOrigin = new THREE.Vector3(van.length / 2, van.width / 2, van.height + 0.35);
    const upArrow = new THREE.ArrowHelper(
      new THREE.Vector3(0, 0, 1),
      upArrowOrigin,
      upArrowLength,
      0x2563eb,
      0.5,
      0.3,
    );
    this.geometryGroup.add(upArrow);

    const upLabelAnchor = new THREE.Object3D();
    upLabelAnchor.position.set(
      upArrowOrigin.x,
      upArrowOrigin.y,
      upArrowOrigin.z + upArrowLength + 0.35,
    );
    this.geometryGroup.add(upLabelAnchor);
    this.labelAnchors.set('orientation-up', {
      object: upLabelAnchor,
      text: 'up',
      color: '#2563eb',
    });

    const placements = this.solution?.placements ?? [];
    const placementColorMap = this.buildPlacementColorMap(
      placements.map((placement) => placement.packageItemId),
    );

    this.placementMeshes = [];
    for (const placement of placements) {
      const packageItem = this.scenario.packages.find((pkg) => pkg.id === placement.packageItemId);
      const color =
        placementColorMap.get(placement.packageItemId) ??
        this.colorFromPackageId(placement.packageItemId, 0);
      const geometry = new THREE.BoxGeometry(
        placement.orientedLength,
        placement.orientedWidth,
        placement.orientedHeight,
      );
      const material = new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: 0.14,
        roughness: 0.45,
        metalness: 0.05,
        transparent: false,
        opacity: 1,
      });

      const box = new THREE.Mesh(geometry, material);
      box.position.set(
        placement.x + placement.orientedLength / 2,
        placement.y + placement.orientedWidth / 2,
        placement.z + placement.orientedHeight / 2,
      );
      box.userData['meta'] = {
        id: placement.packageItemId,
        reference: packageItem?.externalReference ?? placement.packageItemId.slice(0, 8),
        stopIndex: packageItem?.deliveryStopIndex ?? 0,
        position: `(${placement.x.toFixed(1)}, ${placement.y.toFixed(1)}, ${placement.z.toFixed(1)})`,
        size: `${placement.orientedLength.toFixed(1)} × ${placement.orientedWidth.toFixed(1)} × ${placement.orientedHeight.toFixed(1)}`,
        color,
      } satisfies PlacementVisualMeta;
      this.geometryGroup.add(box);
      this.placementMeshes.push(box);
      this.labelAnchors.set(placement.packageItemId, {
        object: box,
        text: packageItem?.externalReference ?? `Box ${this.placementMeshes.length}`,
        color,
      });
    }

    const focus = new THREE.Vector3(van.length / 2, van.width / 2, van.height / 2);
    const maxDim = Math.max(van.length, van.width, van.height);
    this.camera.position.set(van.length * 1.2, -van.width * 1.6, van.height * 1.5 + maxDim * 0.4);
    this.controls.target.copy(focus);
    this.controls.update();
    this.updateLabelOverlays();
  }

  private disposeGroup(): void {
    this.labelAnchors.clear();
    this.placementMeshes = [];

    for (const child of [...this.geometryGroup.children]) {
      this.geometryGroup.remove(child);

      if (child instanceof THREE.Mesh) {
        child.geometry.dispose();
        if (Array.isArray(child.material)) {
          for (const material of child.material) {
            material.dispose();
          }
        } else {
          child.material.dispose();
        }
      }

      if (child instanceof THREE.LineSegments) {
        child.geometry.dispose();
        child.material.dispose();
      }

      if (child instanceof THREE.ArrowHelper) {
        child.line.geometry.dispose();
        (child.line.material as THREE.Material).dispose();
        child.cone.geometry.dispose();
        (child.cone.material as THREE.Material).dispose();
      }
    }
  }

  protected onPointerMove(event: MouseEvent): void {
    if (!this.renderer || this.placementMeshes.length === 0) {
      this.hoverCard = null;
      return;
    }

    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    this.raycaster.setFromCamera(this.pointer, this.camera);
    const intersections = this.raycaster.intersectObjects(this.placementMeshes, false);

    if (intersections.length === 0) {
      this.hoverCard = null;
      return;
    }

    const meta = intersections[0].object.userData['meta'] as PlacementVisualMeta | undefined;
    if (!meta) {
      this.hoverCard = null;
      return;
    }

    this.hoverCard = {
      title: meta.reference,
      subtitle: `Stop ${meta.stopIndex}`,
      detail: `${meta.size} at ${meta.position}`,
      x: event.offsetX + 18,
      y: event.offsetY + 18,
      color: meta.color,
    };
  }

  protected clearHoverCard(): void {
    this.hoverCard = null;
  }

  private updateLabelOverlays(): void {
    if (!this.renderer || !this.scenario) {
      this.labels = [];
      return;
    }

    const canvas = this.canvasRef.nativeElement;
    const nextLabels: OverlayLabel[] = [];

    for (const [id, anchor] of this.labelAnchors.entries()) {
      const screenPoint = anchor.object.position.clone().project(this.camera);
      const visible = screenPoint.z >= -1 && screenPoint.z <= 1;

      nextLabels.push({
        id,
        text: anchor.text,
        color: anchor.color,
        x: ((screenPoint.x + 1) / 2) * canvas.clientWidth,
        y: ((-screenPoint.y + 1) / 2) * canvas.clientHeight - 18,
        visible,
      });
    }

    this.labels = nextLabels;
  }

  private buildPlacementColorMap(packageIds: string[]): Map<string, string> {
    const uniquePackageIds = [...new Set(packageIds)].sort();
    const colorMap = new Map<string, string>();
    const usedColors = new Set<string>();

    for (const packageId of uniquePackageIds) {
      let selectedColor = this.colorFromPackageId(packageId, 0);
      for (let attempt = 0; attempt < 64; attempt++) {
        const candidate = this.colorFromPackageId(packageId, attempt);
        if (!usedColors.has(candidate)) {
          selectedColor = candidate;
          break;
        }
      }

      colorMap.set(packageId, selectedColor);
      usedColors.add(selectedColor);
    }

    return colorMap;
  }

  private colorFromPackageId(packageId: string, attempt: number): string {
    let hash = 0;
    for (let index = 0; index < packageId.length; index++) {
      hash = (hash * 31 + packageId.charCodeAt(index)) | 0;
    }

    const normalizedHash = Math.abs(hash);
    const hueSeed = (normalizedHash + attempt * 137) % 360;
    const saturationOptions = [0.62, 0.68, 0.74, 0.8];
    const lightnessOptions = [0.46, 0.54, 0.62];
    const saturation = saturationOptions[(normalizedHash + attempt) % saturationOptions.length];
    const lightness =
      lightnessOptions[(Math.floor(normalizedHash / 7) + attempt) % lightnessOptions.length];

    return this.hslToHex(hueSeed, saturation, lightness);
  }

  private hslToHex(hue: number, saturation: number, lightness: number): string {
    const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation;
    const segment = hue / 60;
    const secondary = chroma * (1 - Math.abs((segment % 2) - 1));

    let red = 0;
    let green = 0;
    let blue = 0;

    if (segment >= 0 && segment < 1) {
      red = chroma;
      green = secondary;
    } else if (segment >= 1 && segment < 2) {
      red = secondary;
      green = chroma;
    } else if (segment >= 2 && segment < 3) {
      green = chroma;
      blue = secondary;
    } else if (segment >= 3 && segment < 4) {
      green = secondary;
      blue = chroma;
    } else if (segment >= 4 && segment < 5) {
      red = secondary;
      blue = chroma;
    } else {
      red = chroma;
      blue = secondary;
    }

    const match = lightness - chroma / 2;
    const toChannel = (value: number) => Math.round((value + match) * 255);
    const redChannel = toChannel(red).toString(16).padStart(2, '0');
    const greenChannel = toChannel(green).toString(16).padStart(2, '0');
    const blueChannel = toChannel(blue).toString(16).padStart(2, '0');

    return `#${redChannel}${greenChannel}${blueChannel}`;
  }
}
