import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { CategoryService } from '../../../core/services/category.service';
import { Category } from '../../../core/models/job.models';

@Component({
  selector: 'app-manage-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinner],
  templateUrl: './manage-categories.html',
  styleUrls: ['./manage-categories.scss']
})
export class ManageCategories implements OnInit {
  categories: Category[] = [];
  loading = true;
  showModal = false;
  editingId: number | null = null;
  name = '';
  iconFile: File | null = null;
  error = '';

  constructor(private categoryService: CategoryService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.categoryService.getAll().subscribe({
      next: cats => {
        this.categories = cats;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  openCreate() {
    this.editingId = null;
    this.name = '';
    this.iconFile = null;
    this.showModal = true;
    this.error = '';
  }

  openEdit(cat: Category) {
    this.editingId = cat.id;
    this.name = cat.name;
    this.iconFile = null;
    this.showModal = true;
  }

  onIconSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.iconFile = input.files?.[0] ?? null;
  }

  save() {
    if (!this.name.trim()) {
      this.error = 'Name is required.';
      return;
    }

    const onSuccess = () => {
      this.showModal = false;
      this.load();
    };
    const onError = (err: { error?: { detail?: string } }) => {
      this.error = err.error?.detail || 'Save failed.';
    };

    if (this.editingId) {
      this.categoryService.update(this.editingId, this.name, this.iconFile ?? undefined)
        .subscribe({ next: onSuccess, error: onError });
    } else {
      this.categoryService.create(this.name, this.iconFile ?? undefined)
        .subscribe({ next: onSuccess, error: onError });
    }
  }

  deleteCat(id: number) {
    if (!confirm('Delete this category?')) return;
    this.categoryService.delete(id).subscribe({
      next: () => this.load()
    });
  }

  closeModal() {
    this.showModal = false;
  }
}
