import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DraftPackage } from '../../models/draft-scenario.model';
import { PackageEditorComponent } from '../package-editor/package-editor.component';

@Component({
  selector: 'app-package-list',
  standalone: true,
  imports: [CommonModule, PackageEditorComponent],
  templateUrl: './package-list.component.html',
  styleUrl: './package-list.component.scss',
})
export class PackageListComponent {
  @Input() packages: DraftPackage[] = [];
  @Input() disabled = false;
  @Output() removePackage = new EventEmitter<number>();
}
