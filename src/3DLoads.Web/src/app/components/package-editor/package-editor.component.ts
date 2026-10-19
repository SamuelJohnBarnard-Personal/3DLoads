import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DraftPackage, ORIENTATION_OPTIONS } from '../../models/draft-scenario.model';

@Component({
  selector: 'app-package-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './package-editor.component.html',
  styleUrl: './package-editor.component.scss',
})
export class PackageEditorComponent {
  @Input({ required: true }) package!: DraftPackage;
  @Input({ required: true }) index!: number;
  @Input() disabled = false;
  @Output() remove = new EventEmitter<void>();

  protected readonly orientationOptions = ORIENTATION_OPTIONS;
}
