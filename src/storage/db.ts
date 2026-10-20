import type { Project, Workspace } from '../types';

const DB_NAME = 'figma_studio_db';
const DB_VERSION = 2;
const STORE_PROJECTS = 'projects';
const STORE_AUTOSAVE = 'autosave';
const STORE_WORKSPACES = 'workspaces';

const LOCAL_STORAGE_KEY = 'figma_studio_project_v1';
const LOCAL_STORAGE_LIST_KEY = 'figma_studio_project_list_v1';
const LOCAL_STORAGE_WS_KEY = 'figma_studio_workspaces_v1';

export const DEFAULT_WORKSPACES: Workspace[] = [
  {
    id: 'ws-personal',
    name: 'Personal Workspace',
    description: 'Personal experiments, wireframes, and prototypes',
    icon: 'User',
    color: '#0D99FF',
    createdAt: Date.now() - 86400000 * 7,
  },
  {
    id: 'ws-product',
    name: 'Product Design System',
    description: 'Core UI kit, design tokens, and components',
    icon: 'Layers',
    color: '#10B981',
    createdAt: Date.now() - 86400000 * 5,
  },
  {
    id: 'ws-client',
    name: 'Client Projects',
    description: 'Client deliverables, marketing assets, and decks',
    icon: 'Briefcase',
    color: '#F59E0B',
    createdAt: Date.now() - 86400000 * 2,
  },
];

class DeviceStorageDatabase {
  private db: IDBDatabase | null = null;
  private isIndexedDBAvailable = false;

  constructor() {
    this.isIndexedDBAvailable = typeof window !== 'undefined' && 'indexedDB' in window;
  }

  public async init(): Promise<void> {
    if (!this.isIndexedDBAvailable) return;

    return new Promise((resolve) => {
      try {
        const request = window.indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = (event) => {
          const db = (event.target as IDBOpenDBRequest).result;
          if (!db.objectStoreNames.contains(STORE_PROJECTS)) {
            db.createObjectStore(STORE_PROJECTS, { keyPath: 'id' });
          }
          if (!db.objectStoreNames.contains(STORE_AUTOSAVE)) {
            db.createObjectStore(STORE_AUTOSAVE, { keyPath: 'key' });
          }
          if (!db.objectStoreNames.contains(STORE_WORKSPACES)) {
            db.createObjectStore(STORE_WORKSPACES, { keyPath: 'id' });
          }
        };

        request.onsuccess = (event) => {
          this.db = (event.target as IDBOpenDBRequest).result;
          this.seedInitialWorkspaces();
          resolve();
        };

        request.onerror = () => {
          console.warn('IndexedDB failed to open, using localStorage fallback');
          resolve();
        };
      } catch (e) {
        console.warn('IndexedDB initialization error:', e);
        resolve();
      }
    });
  }

  private async seedInitialWorkspaces(): Promise<void> {
    const existing = await this.getAllWorkspaces();
    if (existing.length === 0) {
      for (const ws of DEFAULT_WORKSPACES) {
        await this.saveWorkspace(ws);
      }
    }
  }

  public async saveProject(project: Project): Promise<void> {
    const updatedProject: Project = {
      ...project,
      workspaceId: project.workspaceId || 'ws-personal',
      updatedAt: Date.now(),
      createdAt: project.createdAt || Date.now(),
    };

    // 1. IndexedDB
    if (this.db) {
      await new Promise<void>((resolve, reject) => {
        try {
          const tx = this.db!.transaction(STORE_PROJECTS, 'readwrite');
          const store = tx.objectStore(STORE_PROJECTS);
          const req = store.put(updatedProject);
          req.onsuccess = () => resolve();
          req.onerror = () => reject(req.error);
        } catch (err) {
          reject(err);
        }
      }).catch((e) => console.warn('IndexedDB save failed:', e));
    }

    // 2. localStorage fallback & cache
    try {
      localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(updatedProject));
      const list = this.getLocalStorageProjectList();
      const idx = list.findIndex((p) => p.id === updatedProject.id);
      const meta = {
        id: updatedProject.id,
        name: updatedProject.name,
        workspaceId: updatedProject.workspaceId,
        updatedAt: updatedProject.updatedAt || Date.now(),
        deleted: updatedProject.deleted,
        deletedAt: updatedProject.deletedAt,
      };
      if (idx >= 0) {
        list[idx] = meta;
      } else {
        list.push(meta);
      }
      localStorage.setItem(LOCAL_STORAGE_LIST_KEY, JSON.stringify(list));
    } catch (e) {
      console.warn('localStorage cache failed (quota):', e);
    }
  }

  public async getProject(id: string): Promise<Project | null> {
    if (this.db) {
      try {
        const project = await new Promise<Project | null>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_PROJECTS, 'readonly');
          const store = tx.objectStore(STORE_PROJECTS);
          const req = store.get(id);
          req.onsuccess = () => resolve(req.result || null);
          req.onerror = () => reject(req.error);
        });
        if (project) return project;
      } catch (e) {
        console.warn('Error reading from IndexedDB:', e);
      }
    }

    // Fallback to localStorage
    try {
      const saved = localStorage.getItem(LOCAL_STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (parsed.id === id) return parsed;
      }
    } catch {
      // ignore
    }

    return null;
  }

  public async getAllProjects(): Promise<Project[]> {
    if (this.db) {
      try {
        const projects = await new Promise<Project[]>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_PROJECTS, 'readonly');
          const store = tx.objectStore(STORE_PROJECTS);
          const req = store.getAll();
          req.onsuccess = () => resolve(req.result || []);
          req.onerror = () => reject(req.error);
        });
        if (projects && projects.length > 0) {
          return projects.sort((a, b) => (b.updatedAt || 0) - (a.updatedAt || 0));
        }
      } catch (e) {
        console.warn('Error getting all projects from IndexedDB:', e);
      }
    }

    // Fallback to localStorage
    try {
      const saved = localStorage.getItem(LOCAL_STORAGE_KEY);
      if (saved) {
        return [JSON.parse(saved)];
      }
    } catch {
      // ignore
    }

    return [];
  }

  public async deleteProject(id: string): Promise<void> {
    if (this.db) {
      try {
        await new Promise<void>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_PROJECTS, 'readwrite');
          const store = tx.objectStore(STORE_PROJECTS);
          const req = store.delete(id);
          req.onsuccess = () => resolve();
          req.onerror = () => reject(req.error);
        });
      } catch (e) {
        console.warn('Error deleting from IndexedDB:', e);
      }
    }

    try {
      const list = this.getLocalStorageProjectList().filter((p) => p.id !== id);
      localStorage.setItem(LOCAL_STORAGE_LIST_KEY, JSON.stringify(list));
      const current = localStorage.getItem(LOCAL_STORAGE_KEY);
      if (current && JSON.parse(current).id === id) {
        localStorage.removeItem(LOCAL_STORAGE_KEY);
      }
    } catch {
      // ignore
    }
  }

  public async softDeleteProject(id: string): Promise<void> {
    const project = await this.getProject(id);
    if (project) {
      project.deleted = true;
      project.deletedAt = Date.now();
      await this.saveProject(project);
    }
  }

  public async restoreProject(id: string): Promise<void> {
    const project = await this.getProject(id);
    if (project) {
      project.deleted = false;
      delete project.deletedAt;
      await this.saveProject(project);
    }
  }

  public async emptyBin(): Promise<void> {
    const all = await this.getAllProjects();
    const deletedProjects = all.filter((p) => p.deleted);
    for (const p of deletedProjects) {
      await this.deleteProject(p.id);
    }
  }

  public async saveAutosave(project: Project): Promise<void> {
    if (this.db) {
      try {
        await new Promise<void>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_AUTOSAVE, 'readwrite');
          const store = tx.objectStore(STORE_AUTOSAVE);
          const req = store.put({
            key: 'latest',
            project,
            timestamp: Date.now(),
          });
          req.onsuccess = () => resolve();
          req.onerror = () => reject(req.error);
        });
      } catch (e) {
        console.warn('Autosave to IndexedDB failed:', e);
      }
    }

    try {
      localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(project));
    } catch {
      // ignore
    }
  }

  // Workspaces Management
  public async getAllWorkspaces(): Promise<Workspace[]> {
    if (this.db) {
      try {
        const list = await new Promise<Workspace[]>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_WORKSPACES, 'readonly');
          const store = tx.objectStore(STORE_WORKSPACES);
          const req = store.getAll();
          req.onsuccess = () => resolve(req.result || []);
          req.onerror = () => reject(req.error);
        });
        if (list && list.length > 0) return list;
      } catch (e) {
        console.warn('Error fetching workspaces from DB:', e);
      }
    }

    // Fallback to localStorage
    try {
      const raw = localStorage.getItem(LOCAL_STORAGE_WS_KEY);
      if (raw) return JSON.parse(raw);
    } catch {
      // ignore
    }
    return DEFAULT_WORKSPACES;
  }

  public async saveWorkspace(ws: Workspace): Promise<void> {
    if (this.db) {
      try {
        await new Promise<void>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_WORKSPACES, 'readwrite');
          const store = tx.objectStore(STORE_WORKSPACES);
          const req = store.put(ws);
          req.onsuccess = () => resolve();
          req.onerror = () => reject(req.error);
        });
      } catch (e) {
        console.warn('Error saving workspace to DB:', e);
      }
    }

    try {
      const list = await this.getAllWorkspaces();
      const idx = list.findIndex((w) => w.id === ws.id);
      if (idx >= 0) list[idx] = ws;
      else list.push(ws);
      localStorage.setItem(LOCAL_STORAGE_WS_KEY, JSON.stringify(list));
    } catch {
      // ignore
    }
  }

  public async deleteWorkspace(id: string): Promise<void> {
    if (this.db) {
      try {
        await new Promise<void>((resolve, reject) => {
          const tx = this.db!.transaction(STORE_WORKSPACES, 'readwrite');
          const store = tx.objectStore(STORE_WORKSPACES);
          const req = store.delete(id);
          req.onsuccess = () => resolve();
          req.onerror = () => reject(req.error);
        });
      } catch (e) {
        console.warn('Error deleting workspace:', e);
      }
    }

    try {
      const list = (await this.getAllWorkspaces()).filter((w) => w.id !== id);
      localStorage.setItem(LOCAL_STORAGE_WS_KEY, JSON.stringify(list));
    } catch {
      // ignore
    }
  }

  public async updateProjectWorkspace(projectId: string, workspaceId: string): Promise<void> {
    const project = await this.getProject(projectId);
    if (project) {
      project.workspaceId = workspaceId;
      await this.saveProject(project);
    }
  }

  public async getStorageUsage(): Promise<{ usedBytes: number; quotaBytes: number; projectCount: number }> {
    let usedBytes = 0;
    let quotaBytes = 50 * 1024 * 1024;
    let projectCount = 0;

    if (navigator.storage && navigator.storage.estimate) {
      try {
        const estimate = await navigator.storage.estimate();
        usedBytes = estimate.usage || 0;
        quotaBytes = estimate.quota || quotaBytes;
      } catch {
        // ignore
      }
    }

    try {
      const projects = await this.getAllProjects();
      projectCount = projects.length;
      if (usedBytes === 0) {
        usedBytes = JSON.stringify(projects).length * 2;
      }
    } catch {
      // ignore
    }

    return { usedBytes, quotaBytes, projectCount };
  }

  private getLocalStorageProjectList(): Array<{ id: string; name: string; workspaceId?: string; updatedAt: number }> {
    try {
      const raw = localStorage.getItem(LOCAL_STORAGE_LIST_KEY);
      if (raw) return JSON.parse(raw);
    } catch {
      // ignore
    }
    return [];
  }
}

export const deviceDB = new DeviceStorageDatabase();
