import React, { useState, useEffect, useRef } from 'react';
import {
  X,
  HardDrive,
  Plus,
  Trash2,
  Download,
  Upload,
  Folder,
  Check,
  Clock,
} from 'lucide-react';
import type { Project } from '../types';
import { deviceDB } from '../storage/db';
import { parseAndImportDesignFile } from '../utils/figmaImporter';

interface ProjectsModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentProject: Project;
  onSwitchProject: (project: Project) => void;
  onCreateNewProject: () => void;
  onDeleteProject?: (id: string) => Promise<void> | void;
}

export const ProjectsModal: React.FC<ProjectsModalProps> = ({
  isOpen,
  onClose,
  currentProject,
  onSwitchProject,
  onCreateNewProject,
  onDeleteProject,
}) => {
  const [projects, setProjects] = useState<Project[]>([]);
  const [storageInfo, setStorageInfo] = useState<{ usedBytes: number; quotaBytes: number; projectCount: number }>({
    usedBytes: 0,
    quotaBytes: 0,
    projectCount: 0,
  });
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadProjects = async () => {
    const list = await deviceDB.getAllProjects();
    setProjects(list);
    const usage = await deviceDB.getStorageUsage();
    setStorageInfo(usage);
  };

  useEffect(() => {
    if (isOpen) {
      loadProjects();
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleDelete = async (p: Project, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!window.confirm(`Delete "${p.name}"? This file will be permanently removed from device storage.`)) {
      return;
    }
    if (onDeleteProject) {
      await onDeleteProject(p.id);
      await loadProjects();
      return;
    }
    await deviceDB.deleteProject(p.id);
    const remaining = await deviceDB.getAllProjects();
    setProjects(remaining);
    const usage = await deviceDB.getStorageUsage();
    setStorageInfo(usage);

    if (p.id === currentProject.id) {
      if (remaining.length > 0) {
        onSwitchProject(remaining[0]);
      } else {
        const freshProject: Project = {
          id: `project-${Date.now()}`,
          name: 'Untitled Design',
          workspaceId: 'ws-personal',
          pages: [
            {
              id: 'page-1',
              name: 'Page 1',
              elements: [],
            },
          ],
          activePageId: 'page-1',
          createdAt: Date.now(),
          updatedAt: Date.now(),
        };
        await deviceDB.saveProject(freshProject);
        const updatedList = await deviceDB.getAllProjects();
        setProjects(updatedList);
        onSwitchProject(freshProject);
      }
    }
  };

  const handleExport = (p: Project, e: React.MouseEvent) => {
    e.stopPropagation();
    const dataStr = 'data:application/octet-stream;charset=utf-8,' + encodeURIComponent(JSON.stringify(p, null, 2));
    const downloadAnchor = document.createElement('a');
    downloadAnchor.setAttribute('href', dataStr);
    downloadAnchor.setAttribute('download', `${p.name.toLowerCase().replace(/\s+/g, '_')}.kerf`);
    document.body.appendChild(downloadAnchor);
    downloadAnchor.click();
    downloadAnchor.remove();
  };

  const handleImportFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      const importedProject = await parseAndImportDesignFile(file, currentProject.workspaceId || 'ws-personal');
      await deviceDB.saveProject(importedProject);
      await loadProjects();
      onSwitchProject(importedProject);
      onClose();
    } catch (err: any) {
      alert(err?.message || 'Failed to import design: Invalid file format');
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.7)',
        backdropFilter: 'blur(8px)',
        zIndex: 10000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '640px',
          maxHeight: '85vh',
          backgroundColor: '#1E1E1E',
          border: '1px solid #333333',
          borderRadius: '12px',
          boxShadow: '0 24px 64px rgba(0, 0, 0, 0.8)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          color: '#E0E0E0',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '16px 20px',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#252525',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <HardDrive size={18} color="#0D99FF" />
            <span style={{ fontSize: '14px', fontWeight: 600 }}>Device Storage Projects Database</span>
            <span
              style={{
                fontSize: '11px',
                padding: '2px 8px',
                backgroundColor: 'rgba(16, 185, 129, 0.15)',
                color: '#10B981',
                borderRadius: '12px',
              }}
            >
              IndexedDB Active
            </span>
          </div>
          <button onClick={onClose} className="figma-btn" style={{ padding: '4px', borderRadius: '4px', color: '#888' }}>
            <X size={16} />
          </button>
        </div>

        {/* Storage Health Pill */}
        <div
          style={{
            padding: '12px 20px',
            backgroundColor: '#171717',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            fontSize: '11px',
            color: '#9E9E9E',
          }}
        >
          <div>
            <span>Local DB Usage: </span>
            <strong style={{ color: '#E0E0E0' }}>{formatBytes(storageInfo.usedBytes)}</strong>
            <span style={{ margin: '0 8px' }}>•</span>
            <span>Projects: </span>
            <strong style={{ color: '#E0E0E0' }}>{projects.length}</strong>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              className="figma-btn"
              onClick={() => fileInputRef.current?.click()}
              style={{ fontSize: '11px', padding: '4px 8px' }}
            >
              <Upload size={12} />
              <span>Import .kerf / .figma</span>
            </button>
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleImportFile}
              accept=".kerf,.figma,.fig,.json"
              style={{ display: 'none' }}
            />
            <button
              className="figma-btn figma-btn-primary"
              onClick={() => {
                onCreateNewProject();
                onClose();
              }}
              style={{ fontSize: '11px', padding: '4px 10px' }}
            >
              <Plus size={12} />
              <span>New Project</span>
            </button>
          </div>
        </div>

        {/* Project List */}
        <div style={{ padding: '16px 20px', overflowY: 'auto', flex: 1, maxHeight: '55vh' }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: '12px' }}>
            {projects.map((p) => {
              const isCurrent = p.id === currentProject.id;
              const pageCount = p.pages?.length || 1;
              const elementCount = p.pages?.reduce((acc, pg) => acc + (pg.elements?.length || 0), 0) || 0;
              const updatedStr = p.updatedAt ? new Date(p.updatedAt).toLocaleTimeString() : 'Just now';

              return (
                <div
                  key={p.id}
                  style={{
                    backgroundColor: isCurrent ? 'rgba(13, 153, 255, 0.08)' : '#262626',
                    border: isCurrent ? '1.5px solid #0D99FF' : '1px solid #333333',
                    borderRadius: '8px',
                    padding: '14px',
                    cursor: 'pointer',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                    gap: '10px',
                    transition: 'all 0.15s ease',
                  }}
                  onClick={() => {
                    onSwitchProject(p);
                    onClose();
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Folder size={16} color={isCurrent ? '#0D99FF' : '#9E9E9E'} />
                      <span style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>{p.name}</span>
                    </div>
                    {isCurrent && (
                      <span
                        style={{
                          fontSize: '10px',
                          color: '#0D99FF',
                          backgroundColor: 'rgba(13, 153, 255, 0.2)',
                          padding: '2px 6px',
                          borderRadius: '4px',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '2px',
                        }}
                      >
                        <Check size={10} /> Active
                      </span>
                    )}
                  </div>

                  <div style={{ fontSize: '11px', color: '#888', display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span>{pageCount} page(s)</span>
                    <span>•</span>
                    <span>{elementCount} layers</span>
                    <span>•</span>
                    <span style={{ display: 'flex', alignItems: 'center', gap: '3px' }}>
                      <Clock size={10} /> {updatedStr}
                    </span>
                  </div>

                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'flex-end',
                      gap: '6px',
                      borderTop: '1px solid rgba(255, 255, 255, 0.06)',
                      paddingTop: '8px',
                    }}
                    onClick={(e) => e.stopPropagation()}
                  >
                    <button
                      className="figma-btn"
                      style={{ padding: '4px 6px', fontSize: '11px' }}
                      onClick={(e) => handleExport(p, e)}
                      title="Export as JSON"
                    >
                      <Download size={12} />
                    </button>
                    <button
                      className="figma-btn"
                      style={{
                        padding: '4px 8px',
                        fontSize: '11px',
                        color: '#EF4444',
                        backgroundColor: 'rgba(239, 68, 68, 0.08)',
                        border: '1px solid rgba(239, 68, 68, 0.25)',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                      }}
                      onClick={(e) => handleDelete(p, e)}
                      title="Delete Project from Device Storage"
                    >
                      <Trash2 size={12} />
                      <span>Delete</span>
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '1px solid #2C2C2C',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            backgroundColor: '#252525',
          }}
        >
          <span style={{ fontSize: '11px', color: '#888' }}>
            Data stored locally inside your browser sandbox database.
          </span>
          <button className="figma-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
